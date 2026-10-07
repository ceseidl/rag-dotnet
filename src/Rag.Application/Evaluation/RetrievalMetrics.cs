namespace Rag.Application.Evaluation;

/// <summary>Métricas clássicas de recuperação, por pergunta.
/// "ranked": ids de documento, na ordem recuperada.</summary>
public static class RetrievalMetrics
{
    public static bool Hit(
        IReadOnlyList<string> ranked, ISet<string> relevant) =>
        ranked.Any(relevant.Contains);

    /// <summary>1 / posição do 1º relevante (ou 0).</summary>
    public static double ReciprocalRank(
        IReadOnlyList<string> ranked, ISet<string> relevant)
    {
        for (var i = 0; i < ranked.Count; i++)
            if (relevant.Contains(ranked[i]))
                return 1.0 / (i + 1);
        return 0;
    }

    public static double Recall(
        IReadOnlyList<string> ranked, ISet<string> relevant) =>
        relevant.Count == 0 ? 0 :
        relevant.Count(ranked.Contains) / (double)relevant.Count;
}
