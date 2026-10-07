using System.Runtime.CompilerServices;
using Rag.Application.Abstractions;
using Rag.Domain;

namespace Rag.Infrastructure.Documents;

/// <summary>Lê arquivos de uma pasta (padrão: .md e .txt). Os
/// metadados vêm do frontmatter: title, version, area e access
/// (public, team, restricted). Ignora README.md e arquivos que
/// começam com "_" ou ".". Para PDF, Word ou HTML, escreva outra
/// IDocumentSource que extraia o texto.</summary>
public sealed class FolderDocumentSource(
    string folder, IReadOnlyList<string>? extensions = null)
    : IDocumentSource
{
    private readonly HashSet<string> _ext = new(
        extensions ?? [".md", ".txt"],
        StringComparer.OrdinalIgnoreCase);

    public async IAsyncEnumerable<SourceDocument> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var files = Directory.EnumerateFiles(
                folder, "*", SearchOption.AllDirectories)
            .Where(Accepts).Order();
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
                acc, rel, body,
                meta.GetValueOrDefault("area") ?? "");
        }
    }

    private bool Accepts(string path)
    {
        var name = Path.GetFileName(path);
        return _ext.Contains(Path.GetExtension(path))
            && !name.Equals("README.md",
                StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith('_') && !name.StartsWith('.');
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
