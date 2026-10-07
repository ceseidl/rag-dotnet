using System.Text.Json;
using Rag.Application.Evaluation;
using Rag.Tests.Support;

namespace Rag.Tests;

public class EvaluationTests
{
    [Fact]
    public void Metrics_follow_their_definitions()
    {
        var rel = new HashSet<string> { "b", "z" };
        IReadOnlyList<string> ranked = ["a", "b", "c"];

        Assert.True(RetrievalMetrics.Hit(ranked, rel));
        Assert.Equal(0.5, RetrievalMetrics.ReciprocalRank(ranked, rel));
        Assert.Equal(0.5, RetrievalMetrics.Recall(ranked, rel));
        Assert.Equal(0, RetrievalMetrics.ReciprocalRank(["x"], rel));
    }

    // Valida a MECÂNICA com embeddings por hash de termos. Os números
    // NÃO medem a qualidade semântica de um modelo real: para isso,
    // rode a mesma avaliação com o provedor configurado.
    [Fact]
    public async Task Golden_set_runs_through_the_real_pipeline()
    {
        var path = Path.Combine(Rig.DocsFolder(), "..", "eval",
            "golden.json");
        var golden = JsonSerializer.Deserialize<GoldenQuestion[]>(
            await File.ReadAllTextAsync(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization
                    .JsonStringEnumConverter() }
            })!;
        var rig = await Rig.WithSampleDocsAsync();
        var runner = new EvaluationRunner(rig.Retrieval, rig.Ask);

        var report = await runner.RunAsync(golden);

        Assert.Empty(report.Failures);
        Assert.True(report.HitRate >= 0.9, $"hit rate {report.HitRate}");
        Assert.True(report.Mrr >= 0.8, $"mrr {report.Mrr}");
        Assert.Equal(1.0, report.AbstentionAccuracy);
        Assert.True(report.MeanGrounding >= 0.9);
    }
}
