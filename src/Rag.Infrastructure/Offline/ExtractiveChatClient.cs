using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Rag.Application;
using Rag.Application.Querying;

namespace Rag.Infrastructure.Offline;

/// <summary>FAKE determinístico: não é um LLM. Escolhe a frase do
/// contexto que mais se parece com a pergunta e cita o trecho.
/// Serve para testar o encanamento (prompt e citações).</summary>
public sealed partial class ExtractiveChatClient : IChatClient
{
    [GeneratedRegex(
        "<documento id=\"([0-9]+)\"[^>]*>\n(.*?)\n</documento>",
        RegexOptions.Singleline)]
    private static partial Regex Doc();

    [GeneratedRegex("<pergunta>(.*?)</pergunta>",
        RegexOptions.Singleline)]
    private static partial Regex Question();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var user = messages
            .Last(m => m.Role == ChatRole.User).Text;
        var q = TextTokens.Of(
            Question().Match(user).Groups[1].Value).ToHashSet();

        var best = Doc().Matches(user)
            .SelectMany(m => Regex
                .Split(m.Groups[2].Value, @"(?<=[.!?])\s+|\n+")
                .Select(s => (n: m.Groups[1].Value, s: s.Trim())))
            .Where(x => x.s.Length > 20)
            .Select(x => (x.n, x.s,
                hit: TextTokens.Of(x.s).Count(q.Contains)))
            .OrderByDescending(x => x.hit).FirstOrDefault();

        var text = best.hit >= 2
            ? $"{best.s} [{best.n}]"
            : PromptBuilder.NoAnswer;
        var usage = new UsageDetails
        {
            InputTokenCount = user.Length / 4,
            OutputTokenCount = text.Length / 4
        };
        return Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, text))
        { Usage = usage, ModelId = "extractive-fake" });
    }

    public IAsyncEnumerable<ChatResponseUpdate>
        GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type type, object? key = null) =>
        key is null && type.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
