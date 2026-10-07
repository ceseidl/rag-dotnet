using Rag.Application;
using Rag.Application.Ingestion;
using Rag.Domain;
using Rag.Tests.Support;

namespace Rag.Tests;

public class ChunkerAndIngestionTests
{
    private static string LongText() => string.Join("\n\n",
        Enumerable.Range(1, 12).Select(i =>
            $"Parágrafo {i}: " + string.Join(' ',
                Enumerable.Range(1, 22).Select(w => $"palavra{i}x{w}"))));

    [Fact]
    public void Chunks_respect_size_and_overlap()
    {
        var opt = new RagOptions { ChunkSize = 400, ChunkOverlap = 80 };
        var doc = Rig.Doc("d", "# T\n\n" + LongText());

        var chunks = new MarkdownChunker(opt).Split(doc, "h");

        Assert.True(chunks.Count > 3);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 400 + 80 + 2));
        for (var i = 1; i < chunks.Count; i++)
        {
            // as últimas palavras do trecho anterior reaparecem
            var tail = chunks[i - 1].Text.Split(' ')[^1];
            Assert.Contains(tail, chunks[i].Text);
        }
    }

    [Fact]
    public void Chunks_carry_metadata_and_section()
    {
        var doc = Rig.Doc("politica", "# Doc\n\n## Janelas\n\nTexto A.\n\n" +
            "## Aprovação\n\nTexto B.", AccessLevel.Restricted, "2.3");

        var chunks = new MarkdownChunker(new RagOptions()).Split(doc, "h");

        Assert.Equal(["Janelas", "Aprovação"],
            chunks.Select(c => c.Section));
        Assert.All(chunks, c =>
        {
            Assert.Equal("2.3", c.Version);
            Assert.Equal(AccessLevel.Restricted, c.Access);
            Assert.Equal(Hashing.Sha256(c.Text), c.Hash);
        });
        Assert.Equal("politica#1", chunks[1].Id);
    }

    [Fact]
    public void Code_fence_does_not_start_a_section()
    {
        var doc = Rig.Doc("d", "# T\n\n```\n# comentario\n```\n\nfim");
        var chunks = new MarkdownChunker(new RagOptions()).Split(doc, "h");
        Assert.Single(chunks);
        Assert.Contains("# comentario", chunks[0].Text);
    }

    [Fact]
    public async Task Reindexes_only_what_changed()
    {
        var rig = new Rig([Rig.Doc("a", "# A\n\nConteúdo da politica A."),
            Rig.Doc("b", "# B\n\nConteúdo do runbook B.")]);

        var first = await rig.Ingestion.RunAsync();
        var second = await rig.Ingestion.RunAsync();
        rig.Source.Docs[1] = Rig.Doc("b", "# B\n\nTexto novo do B.");
        var third = await rig.Ingestion.RunAsync();

        Assert.Equal((2, 0, 0), (first.Added, first.Updated, first.Unchanged));
        Assert.Equal((0, 0, 2), (second.Added, second.Updated, second.Unchanged));
        Assert.Equal((0, 1, 1), (third.Added, third.Updated, third.Unchanged));
    }

    [Fact]
    public async Task Changing_version_or_access_reindexes()
    {
        var rig = new Rig([Rig.Doc("a", "# A\n\nTexto.")]);
        await rig.Ingestion.RunAsync();

        rig.Source.Docs[0] =
            Rig.Doc("a", "# A\n\nTexto.", AccessLevel.Restricted);

        Assert.Equal(1, (await rig.Ingestion.RunAsync()).Updated);
    }

    [Fact]
    public async Task Changing_the_embedding_model_reindexes()
    {
        var docs = new[] { Rig.Doc("a", "# A\n\nTexto.") };
        var first = new Rig(docs, embedder: new NamedEmbedder("m1"));
        await first.Ingestion.RunAsync();

        var second = new Rig(docs, embedder: new NamedEmbedder("m2"),
            store: first.Store);

        Assert.Equal(1, (await second.Ingestion.RunAsync()).Updated);
    }

    [Fact]
    public async Task Removes_documents_that_left_the_source()
    {
        var rig = new Rig([Rig.Doc("a", "# A\n\nUm."),
            Rig.Doc("b", "# B\n\nDois.")]);
        await rig.Ingestion.RunAsync();

        rig.Source.Docs.RemoveAt(1);
        var report = await rig.Ingestion.RunAsync();

        Assert.Equal(1, report.Removed);
        Assert.Equal(["a"], await rig.Store.ListDocumentIdsAsync());
    }

    [Fact]
    public async Task Embeds_in_batches()
    {
        var counter = new CountingEmbedder();
        var opt = new RagOptions { EmbeddingBatchSize = 2, ChunkSize = 100 };
        var rig = new Rig([Rig.Doc("a", "# A\n\n" + LongText())],
            embedder: counter, options: opt);

        var report = await rig.Ingestion.RunAsync();

        Assert.True(report.Chunks > 4);
        Assert.All(counter.BatchSizes, n => Assert.True(n <= 2));
        Assert.Equal(report.Chunks, counter.BatchSizes.Sum());
    }
}
