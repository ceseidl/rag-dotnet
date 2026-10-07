using System.Text;
using System.Text.RegularExpressions;
using Rag.Domain;

namespace Rag.Application.Ingestion;

/// <summary>Divide o Markdown por seção e empacota parágrafos em
/// janelas de ~ChunkSize caracteres, com sobreposição.</summary>
public sealed partial class MarkdownChunker(RagOptions options)
{
    [GeneratedRegex(@"^(#{1,3})\s+(.+?)\s*$")]
    private static partial Regex Heading();

    public string Fingerprint =>
        $"{options.ChunkSize}/{options.ChunkOverlap}";

    public IReadOnlyList<Chunk> Split(
        SourceDocument doc, string documentHash)
    {
        var result = new List<Chunk>();
        foreach (var (section, body) in Sections(doc))
        {
            foreach (var text in Pack(body))
            {
                var index = result.Count;
                result.Add(new Chunk(
                    $"{doc.Id}#{index}", doc.Id, index, doc.Title,
                    section, doc.Version, doc.Source, doc.Access,
                    text, Hashing.Sha256(text), documentHash));
            }
        }
        return result;
    }

    private static IEnumerable<(string, string)> Sections(
        SourceDocument doc)
    {
        var title = doc.Title;
        var body = new StringBuilder();
        var fence = false;
        foreach (var raw in doc.Content.Replace("\r\n", "\n")
                     .Split('\n'))
        {
            if (raw.TrimStart().StartsWith("```")) fence = !fence;
            var m = fence ? null : Heading().Match(raw);
            if (m is { Success: true })
            {
                if (body.ToString().Trim().Length > 0)
                    yield return (title, body.ToString());
                title = m.Groups[2].Value;
                body.Clear();
            }
            else body.AppendLine(raw);
        }
        if (body.ToString().Trim().Length > 0)
            yield return (title, body.ToString());
    }

    private IEnumerable<string> Pack(string body)
    {
        var current = new StringBuilder();
        var overlap = "";
        foreach (var piece in Pieces(body))
        {
            var size = current.Length + piece.Length + 2;
            if (HasText(current) && size > options.ChunkSize)
            {
                var text = current.ToString().Trim();
                yield return text;
                overlap = Tail(text, options.ChunkOverlap);
                current.Clear().Append(overlap).Append(' ');
            }
            if (current.Length > 0 && current[^1] != ' ')
                current.Append("\n\n");
            current.Append(piece);
        }
        var last = current.ToString().Trim();
        if (last.Length > 0 && last != overlap.Trim())
            yield return last;
    }

    // Parágrafos; o parágrafo grande é quebrado por palavras.
    private IEnumerable<string> Pieces(string body)
    {
        foreach (var p in Regex.Split(body.Trim(), @"\n\s*\n"))
        {
            var para = p.Trim();
            if (para.Length == 0) continue;
            if (para.Length <= options.ChunkSize)
            {
                yield return para;
                continue;
            }
            var words = para.Split(
                ' ', StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var w in words)
            {
                if (sb.Length + w.Length + 1 > options.ChunkSize)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                sb.Append(sb.Length == 0 ? "" : " ").Append(w);
            }
            if (sb.Length > 0) yield return sb.ToString();
        }
    }

    private static bool HasText(StringBuilder sb) =>
        sb.ToString().AsSpan().Trim().Length > 0;

    private static string Tail(string text, int size)
    {
        if (size <= 0) return "";
        if (text.Length <= size) return text;
        var cut = text.Length - size;
        var space = text.IndexOf(' ', cut);
        return space < 0 ? "" : text[(space + 1)..];
    }
}
