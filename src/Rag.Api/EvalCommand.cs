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
        await sp.GetRequiredService<IngestionService>().RunAsync();
        var r = await sp.GetRequiredService<EvaluationRunner>()
            .RunAsync(golden);

        Console.WriteLine($"perguntas        {r.Questions}");
        Console.WriteLine($"hit rate         {r.HitRate:P0}");
        Console.WriteLine($"MRR              {r.Mrr:0.00}");
        Console.WriteLine($"recall@k         {r.RecallAtK:P0}");
        Console.WriteLine($"abstenção certa  {r.AbstentionAccuracy:P0}");
        Console.WriteLine($"ancoragem média  {r.MeanGrounding:P0}");
        r.Failures.ToList().ForEach(f => Console.WriteLine("  - " + f));
        return r.Failures.Count == 0 ? 0 : 1;
    }
}
