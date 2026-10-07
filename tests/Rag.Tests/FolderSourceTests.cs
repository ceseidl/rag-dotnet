using Rag.Domain;
using Rag.Infrastructure.Documents;

namespace Rag.Tests;

public class FolderSourceTests
{
    [Fact]
    public async Task Reads_md_and_txt_with_metadata_and_skips_the_rest()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "a.md"),
            "---\ntitle: Doc A\nversion: 2\narea: infra\n" +
            "access: restricted\n---\n# A\n\nTexto A.");
        File.WriteAllText(Path.Combine(dir, "b.txt"), "Texto B.");
        File.WriteAllText(Path.Combine(dir, "README.md"), "ignorar");
        File.WriteAllText(Path.Combine(dir, "_rascunho.md"), "x");
        File.WriteAllText(Path.Combine(dir, "c.pdf"), "x");

        var docs = new List<SourceDocument>();
        await foreach (var d in new FolderDocumentSource(dir)
                           .ReadAllAsync())
            docs.Add(d);
        Directory.Delete(dir, true);

        Assert.Equal(["a", "b"], docs.Select(d => d.Id));
        Assert.Equal(("Doc A", "2", "infra", AccessLevel.Restricted),
            (docs[0].Title, docs[0].Version, docs[0].Area,
                docs[0].Access));
        Assert.Equal(("b", "1", AccessLevel.Public),
            (docs[1].Title, docs[1].Version, docs[1].Access));
    }
}
