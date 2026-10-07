using Rag.Application.Querying;
using Rag.Domain;

namespace Rag.Application.Evaluation;

public sealed record GoldenQuestion(
    string Question,
    string[] ExpectedDocuments,
    bool ShouldAbstain = false,
    AccessLevel Access = AccessLevel.Team);

public sealed record EvaluationReport(
    int Questions,
    double HitRate,
    double Mrr,
    double RecallAtK,
    double AbstentionAccuracy,
    double MeanGrounding,
    IReadOnlyList<string> Failures);

/// <summary>Roda o conjunto-ouro no pipeline real (o da API) e
/// calcula hit rate, MRR, recall@k e abstenção correta.</summary>
public sealed class EvaluationRunner(
    RetrievalService retrieval, AskService ask)
{
    public async Task<EvaluationReport> RunAsync(
        IReadOnlyList<GoldenQuestion> golden,
        CancellationToken ct = default)
    {
        var fails = new List<string>();
        var hits = new List<double>();
        var rr = new List<double>();
        var rec = new List<double>();
        var abst = new List<double>();
        var grounding = new List<double>();

        foreach (var q in golden)
        {
            var result = await ask.AskAsync(
                new(q.Question, q.Access), ct);
            if (q.ShouldAbstain)
            {
                var ok = !result.Answered;
                abst.Add(ok ? 1 : 0);
                if (!ok)
                    fails.Add($"deveria abster: {q.Question}");
                continue;
            }

            var found = await retrieval.RetrieveAsync(
                q.Question, q.Access, ct);
            var ranked = found.Selected
                .Select(s => s.Chunk.DocumentId)
                .Distinct().ToList();
            var rel = q.ExpectedDocuments.ToHashSet();
            hits.Add(RetrievalMetrics.Hit(ranked, rel) ? 1 : 0);
            rr.Add(RetrievalMetrics.ReciprocalRank(ranked, rel));
            rec.Add(RetrievalMetrics.Recall(ranked, rel));
            if (!hits[^1].Equals(1.0))
                fails.Add($"não recuperou: {q.Question}");
            if (result.Answered)
                grounding.Add(result.Diagnostics.Grounding);
        }
        return new(golden.Count, Avg(hits), Avg(rr), Avg(rec),
            Avg(abst), Avg(grounding), fails);
    }

    private static double Avg(List<double> v) =>
        v.Count == 0 ? 0 : v.Average();
}
