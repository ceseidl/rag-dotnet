using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Rag.Domain;

namespace Rag.Application.Querying;

public sealed record AskRequest(
    string Question, AccessLevel Access);

public sealed record AskDiagnostics(
    int Candidates, int Used, int Blocked,
    long? InputTokens, long? OutputTokens,
    double Grounding, IReadOnlyList<int> InvalidCitations);

public sealed record AskResult(
    string Answer,
    bool Answered,
    IReadOnlyList<Citation> Citations,
    AskDiagnostics Diagnostics);

/// <summary>Pipeline de CONSULTA: recuperar, proteger o contexto,
/// montar o prompt, gerar e validar a resposta.</summary>
public sealed class AskService(
    RetrievalService retrieval,
    IChatClient chat,
    RagOptions options,
    ILogger<AskService> log)
{
    public async Task<AskResult> AskAsync(
        AskRequest req, CancellationToken ct = default)
    {
        using var all = RagTelemetry.Start("ask");
        var found = await retrieval.RetrieveAsync(
            req.Question, req.Access, ct);
        var ctx = ContextGuard.Apply(
            found.Selected, options.MaxContextChars);

        // Sem contexto suficiente: nem chama o modelo.
        if (ctx.Chunks.Count == 0)
        {
            RagTelemetry.Abstentions.Add(1);
            return Abstain(found.Candidates, ctx.Blocked);
        }

        var messages = PromptBuilder.Build(
            req.Question, ctx.Chunks);
        ChatResponse reply;
        using (RagTelemetry.Start("generate"))
        {
            reply = await chat.GetResponseAsync(
                messages, ChatSettings(), ct);
        }
        return Validate(reply, ctx, found.Candidates);
    }

    private ChatOptions ChatSettings()
    {
        var o = new ChatOptions
        {
            MaxOutputTokens = options.MaxOutputTokens
        };
        if (options.SendTemperature) o.Temperature = 0;
        if (Enum.TryParse<ReasoningEffort>(
                options.ReasoningEffort, true, out var effort))
            o.Reasoning = new() { Effort = effort };
        return o;
    }

    private AskResult Validate(
        ChatResponse reply, GuardedContext ctx, int candidates)
    {
        var answer = reply.Text.Trim();
        log.LogDebug("Resposta bruta: {Answer}", answer);
        var cited = Groundedness.CitedNumbers(answer);
        var invalid = cited
            .Where(n => n < 1 || n > ctx.Chunks.Count).ToList();
        var refused = answer.Contains(PromptBuilder.NoAnswer);
        var text = string.Join('\n',
            ctx.Chunks.Select(c => c.Chunk.Text));
        var grounding = refused
            ? 1 : Groundedness.Coverage(answer, text);

        var citations = cited.Except(invalid).Select(n =>
        {
            var s = ctx.Chunks[n - 1];
            var c = s.Chunk;
            return new Citation(n, c.DocumentId, c.Title,
                c.Section, c.Version, c.Source,
                Math.Round(s.Score, 3));
        }).ToList();

        // Resposta sem fonte válida não passa como resposta.
        var answered = !refused && citations.Count > 0
            && invalid.Count == 0;
        if (!answered && !refused)
            log.LogWarning("Resposta sem citação descartada");

        var usage = reply.Usage;
        RagTelemetry.Tokens.Add(usage?.InputTokenCount ?? 0,
            new KeyValuePair<string, object?>("dir", "in"));
        RagTelemetry.Tokens.Add(usage?.OutputTokenCount ?? 0,
            new KeyValuePair<string, object?>("dir", "out"));
        if (!answered) RagTelemetry.Abstentions.Add(1);

        var diag = new AskDiagnostics(
            candidates, ctx.Chunks.Count, ctx.Blocked,
            usage?.InputTokenCount, usage?.OutputTokenCount,
            grounding, invalid);
        return answered
            ? new(answer, true, citations, diag)
            : new(PromptBuilder.NoAnswer, false, [], diag);
    }

    private static AskResult Abstain(
        int candidates, int blocked) =>
        new(PromptBuilder.NoAnswer, false, [],
            new(candidates, 0, blocked, null, null, 1, []));
}
