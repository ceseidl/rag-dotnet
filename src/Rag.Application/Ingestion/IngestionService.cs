using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Rag.Application.Abstractions;

namespace Rag.Application.Ingestion;

public sealed record IngestionReport(
    int Added, int Updated, int Unchanged,
    int Removed, int Chunks);

/// <summary>Pipeline de INGESTÃO: ler, dividir, vetorizar em lote
/// e gravar. Só reprocessa o documento cujo hash mudou.</summary>
public sealed class IngestionService(
    IDocumentSource source,
    MarkdownChunker chunker,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IChunkStore store,
    RagOptions options,
    ILogger<IngestionService> log)
{
    public async Task<IngestionReport> RunAsync(
        CancellationToken ct = default)
    {
        using var stage = RagTelemetry.Start("ingest");
        var model = embedder.GetService<
            EmbeddingGeneratorMetadata>()?.DefaultModelId;
        int added = 0, updated = 0, same = 0, chunksTotal = 0;
        var seen = new HashSet<string>();

        await foreach (var doc in source.ReadAllAsync(ct))
        {
            seen.Add(doc.Id);
            // O hash cobre conteúdo, metadados, chunking e
            // modelo: mudar qualquer um deles reindexa.
            var docHash = Hashing.Sha256(string.Join('|',
                doc.Title, doc.Version, doc.Access, doc.Content,
                chunker.Fingerprint, model));
            var current = await store
                .GetDocumentHashAsync(doc.Id, ct);
            if (current == docHash) { same++; continue; }

            var chunks = chunker.Split(doc, docHash);
            var vectors = await EmbedAsync(chunks, ct);
            await store.ReplaceDocumentAsync(doc.Id, vectors, ct);
            chunksTotal += chunks.Count;
            if (current is null) added++; else updated++;
            log.LogInformation("Indexado {Doc}: {N} trechos",
                doc.Id, chunks.Count);
        }

        var removed = 0;
        foreach (var id in await store.ListDocumentIdsAsync(ct))
        {
            if (seen.Contains(id)) continue;
            await store.DeleteDocumentAsync(id, ct);
            removed++;
        }
        stage.Tag("rag.chunks", chunksTotal);
        return new(added, updated, same, removed, chunksTotal);
    }

    // Vetoriza em lotes: menos chamadas, menos risco de 429.
    private async Task<IReadOnlyList<IndexedChunk>> EmbedAsync(
        IReadOnlyList<Domain.Chunk> chunks, CancellationToken ct)
    {
        var items = new List<IndexedChunk>(chunks.Count);
        var size = options.EmbeddingBatchSize;
        foreach (var batch in chunks.Chunk(size))
        {
            var result = await embedder.GenerateAsync(
                batch.Select(c => c.EmbeddingInput).ToList(),
                cancellationToken: ct);
            for (var i = 0; i < batch.Length; i++)
                items.Add(new(batch[i], result[i].Vector));
        }
        return items;
    }
}
