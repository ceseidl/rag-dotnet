using System.Globalization;
using System.Text;

namespace Rag.Application;

/// <summary>Tokenização simples (minúsculas, sem acento, sem
/// stopwords, radical grosseiro). Usada na checagem de
/// groundedness e nos fakes offline de teste.</summary>
public static class TextTokens
{
    private static readonly HashSet<string> Stop = new(
        ("a o as os um uma de da do das dos em no na nos nas "
        + "por para com sem que e ou se eu ao aos como qual "
        + "quais quando onde ser sao foi mais mas ja tem ter "
        + "deve devem pode posso preciso pra isso este esta "
        + "esse essa").Split(' '));

    public static IEnumerable<string> Of(string text)
    {
        var sb = new StringBuilder();
        var form = text.Normalize(NormalizationForm.FormD);
        foreach (var ch in form)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch)
                ? char.ToLowerInvariant(ch) : ' ');
        }
        var words = sb.ToString().Split(
            ' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var w in words)
        {
            if (w.Length < 2 || Stop.Contains(w)) continue;
            yield return Stem(w);
        }
    }

    private static string Stem(string w)
    {
        foreach (var suf in new[] { "coes", "mente", "acao", "ar",
                     "es", "s" })
            if (w.Length > suf.Length + 3 && w.EndsWith(suf))
                return w[..^suf.Length];
        return w;
    }
}
