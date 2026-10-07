using System.Text.RegularExpressions;

namespace Rag.Application.Querying;

/// <summary>Checagens baratas sobre a resposta, sem outra chamada
/// ao modelo: citações válidas e cobertura do contexto.</summary>
public static partial class Groundedness
{
    [GeneratedRegex(@"\[(\d{1,2})\]")]
    private static partial Regex CitationRef();

    public static IReadOnlyList<int> CitedNumbers(
        string answer) =>
        CitationRef().Matches(answer)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct().Order().ToList();

    /// <summary>Fração das frases da resposta cujos termos estão
    /// (em maioria) no contexto. Mede ancoragem lexical, NÃO
    /// verdade; para o sentido, use um avaliador.</summary>
    public static double Coverage(string answer, string context)
    {
        var ctx = TextTokens.Of(context).ToHashSet();
        var sentences = Regex.Split(
                CitationRef().Replace(answer, ""),
                @"(?<=[.!?])\s+")
            .Select(s => TextTokens.Of(s).ToList())
            .Where(t => t.Count > 0).ToList();
        if (sentences.Count == 0) return 0;
        var ok = sentences.Count(t =>
            t.Count(ctx.Contains) / (double)t.Count >= 0.6);
        return ok / (double)sentences.Count;
    }
}
