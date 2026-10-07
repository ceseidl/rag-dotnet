using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Application.Evaluation;
using Rag.Application.Ingestion;
using Rag.Application.Querying;
using Rag.Domain;

namespace Rag.Api;

/// <summary>dotnet run --project src/Rag.Api -- --eval
/// Indexa os documentos e roda o conjunto-ouro com o provedor
/// configurado (fakes offline ou o modelo real).</summary>
public static class EvalCommand
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(WebApplication app)
    {
        var path = app.Configuration["Eval:GoldenSet"]
            ?? Path.Combine(AppContext.BaseDirectory,
                "golden.json");
        var golden = JsonSerializer
            .Deserialize<GoldenQuestion[]>(
                await File.ReadAllTextAsync(path), Json)!;

        var sp = app.Services;
        var ingest = sp.GetRequiredService<IngestionService>();
        await ingest.RunAsync();
        var runner = sp.GetRequiredService<EvaluationRunner>();
        var r = await runner.RunAsync(golden);

        void Show(string k, string v) =>
            Console.WriteLine($"{k,-17}{v}");
        foreach (var q in r.Results)
            Console.WriteLine(
                $"{q.Ms / 1000,5:0.0} s  " +
                $"{(q.Answered ? "resp" : "nao ")}  " +
                $"{q.Ranked.FirstOrDefault() ?? "-"}  " +
                q.Question);
        Show("perguntas", $"{r.Questions}");
        Show("hit rate", $"{r.HitRate:P0}");
        Show("MRR", $"{r.Mrr:0.00}");
        Show("recall@k", $"{r.RecallAtK:P0}");
        Show("abstenção certa", $"{r.AbstentionAccuracy:P0}");
        Show("ancoragem média", $"{r.MeanGrounding:P0}");
        Show("respondidas", $"{r.AnsweredRate:P0}");
        Show("tempo médio", $"{r.MeanAskMs / 1000:0.0} s");
        foreach (var f in r.Failures)
            Console.WriteLine("  - " + f);
        return r.Failures.Count == 0 ? 0 : 1;
    }
}

/// <summary>dotnet run --project src/Rag.Api -- --ask "pergunta"
/// [--level Team]. Indexa, pergunta uma vez e imprime a resposta,
/// as fontes e os tempos, sem HTTP.</summary>
public static class AskCommand
{
    public static async Task<int> RunAsync(
        WebApplication app, string[] args)
    {
        var i = Array.IndexOf(args, "--ask");
        var question = args.ElementAtOrDefault(i + 1) ?? "";
        var j = Array.IndexOf(args, "--level");
        var level = j >= 0 && Enum.TryParse<AccessLevel>(
            args.ElementAtOrDefault(j + 1), true, out var l)
            ? l : AccessLevel.Team;

        var sp = app.Services;
        var clock = Stopwatch.StartNew();
        var report = await sp
            .GetRequiredService<IngestionService>().RunAsync();
        Console.WriteLine($"ingestão: {report.Chunks} trechos " +
            $"em {clock.Elapsed.TotalSeconds:0.0} s");

        clock.Restart();
        var r = await sp.GetRequiredService<AskService>()
            .AskAsync(new(question, level));
        Console.WriteLine($"pergunta: {question}");
        Console.WriteLine($"resposta: {r.Answer}");
        foreach (var c in r.Citations)
            Console.WriteLine($"  [{c.Number}] {c.Source} " +
                $"v{c.Version} > {c.Section} ({c.Score})");
        var secs = clock.Elapsed.TotalSeconds;
        Console.WriteLine($"tempo: {secs:0.0} s" +
            $" · trechos: {r.Diagnostics.Used}" +
            $" · tokens: {r.Diagnostics.InputTokens}" +
            $"/{r.Diagnostics.OutputTokens}");
        return 0;
    }
}
