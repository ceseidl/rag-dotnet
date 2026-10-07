using System.Runtime.CompilerServices;
using Rag.Application.Abstractions;
using Rag.Domain;

namespace Rag.Infrastructure.Documents;

/// <summary>Lê .md de uma pasta. Metadados no frontmatter:
/// title, version e access (public, team, restricted).</summary>
public sealed class MarkdownFolderSource(string folder)
    : IDocumentSource
{
    public async IAsyncEnumerable<SourceDocument> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var files = Directory.EnumerateFiles(
            folder, "*.md", SearchOption.AllDirectories).Order();
        foreach (var path in files)
        {
            var raw = await File.ReadAllTextAsync(path, ct);
            var (meta, body) = Parse(raw);
            var id = Path.GetFileNameWithoutExtension(path);
            var rel = Path.GetRelativePath(folder, path)
                .Replace(Path.DirectorySeparatorChar, '/');
            Enum.TryParse<AccessLevel>(
                meta.GetValueOrDefault("access"),
                true, out var acc);
            yield return new SourceDocument(id,
                meta.GetValueOrDefault("title") ?? id,
                meta.GetValueOrDefault("version") ?? "1",
                acc, rel, body);
        }
    }

    public static (Dictionary<string, string>, string) Parse(
        string raw)
    {
        var text = raw.Replace("\r\n", "\n");
        var meta = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        if (!text.StartsWith("---\n")) return (meta, text);
        var end = text.IndexOf(
            "\n---", 4, StringComparison.Ordinal);
        if (end < 0) return (meta, text);
        foreach (var line in text[4..end].Split('\n'))
        {
            var i = line.IndexOf(':');
            if (i > 0)
                meta[line[..i].Trim()] = line[(i + 1)..].Trim();
        }
        return (meta, text[(end + 4)..].TrimStart('\n'));
    }
}
