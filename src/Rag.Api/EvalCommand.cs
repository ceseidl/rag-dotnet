using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Application.Evaluation;
using Rag.Application.Ingestion;

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
        Show("perguntas", $"{r.Questions}");
        Show("hit rate", $"{r.HitRate:P0}");
        Show("MRR", $"{r.Mrr:0.00}");
        Show("recall@k", $"{r.RecallAtK:P0}");
        Show("abstenção certa", $"{r.AbstentionAccuracy:P0}");
        Show("ancoragem média", $"{r.MeanGrounding:P0}");
        foreach (var f in r.Failures)
            Console.WriteLine("  - " + f);
        return r.Failures.Count == 0 ? 0 : 1;
    }
}
