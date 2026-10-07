using Rag.Application.Abstractions;

namespace Rag.Application.Querying;

/// <summary>Maximal Marginal Relevance: escolhe trechos
/// relevantes E diferentes entre si.</summary>
public static class Mmr
{
    public static IReadOnlyList<ScoredChunk> Select(
        IReadOnlyList<ScoredChunk> pool, int k, double lambda)
    {
        var left = pool.OrderByDescending(c => c.Score).ToList();
        var picked = new List<ScoredChunk>();
        while (picked.Count < k && left.Count > 0)
        {
            var best = left
                .Select(c => (c, v: lambda * c.Score
                    - (1 - lambda) * MaxSimilarity(c, picked)))
                .MaxBy(x => x.v).c;
            picked.Add(best);
            left.Remove(best);
        }
        return picked;
    }

    private static double MaxSimilarity(
        ScoredChunk c, List<ScoredChunk> picked) =>
        picked.Count == 0 ? 0 : picked.Max(p =>
            Cosine(c.Vector.Span, p.Vector.Span));

    public static double Cosine(
        ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }
}
