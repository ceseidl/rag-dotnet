using System.Text.RegularExpressions;
using Rag.Application.Abstractions;

namespace Rag.Application.Querying;

public sealed record GuardedContext(
    IReadOnlyList<ScoredChunk> Chunks, int Blocked, int Trimmed);

/// <summary>Defesa em camadas contra injeção de prompt vinda do
/// conteúdo recuperado e limite de tamanho do contexto.</summary>
public static partial class ContextGuard
{
    // Heurística: pega o óbvio. NÃO é garantia; a defesa real é a
    // soma de delimitadores, prompt, sem ferramentas e checagem.
    [GeneratedRegex(
        @"ignor(e|ar|em)\s+(todas?\s+)?(as\s+)?" +
        @"(instru|regras|orienta)" +
        @"|desconsidere|esque[çc]a\s+(tudo|as\s+instru)" +
        @"|voc[êe]\s+agora\s+[ée]" +
        @"|revele\s+(o\s+)?(prompt|segredo)" +
        @"|ignore\s+(all\s+)?(previous|prior|above)" +
        @"|disregard\s+(the\s+)?(system|previous)" +
        @"|system\s*prompt",
        RegexOptions.IgnoreCase)]
    private static partial Regex Suspicious();

    [GeneratedRegex(@"</?\s*(documento|contexto|pergunta)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex OurTags();

    public static bool LooksLikeInjection(string text) =>
        Suspicious().IsMatch(text);

    /// <summary>Neutraliza as tags do prompt no texto.</summary>
    public static string Neutralize(string text) =>
        OurTags().Replace(text, m => "‹" + m.Value[1..]);

    public static GuardedContext Apply(
        IReadOnlyList<ScoredChunk> chunks, int maxChars)
    {
        var kept = new List<ScoredChunk>();
        int blocked = 0, trimmed = 0, used = 0;
        foreach (var c in chunks)
        {
            if (LooksLikeInjection(c.Chunk.Text))
            {
                blocked++;
                RagTelemetry.Blocked.Add(1);
                continue;
            }
            if (used + c.Chunk.Text.Length > maxChars)
            {
                trimmed++;
                continue;
            }
            used += c.Chunk.Text.Length;
            kept.Add(c);
        }
        return new(kept, blocked, trimmed);
    }
}
