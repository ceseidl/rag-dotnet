using Microsoft.Extensions.VectorData;
using Rag.Application.Abstractions;
using Rag.Domain;

namespace Rag.Infrastructure.Vectors;

/// <summary>Adaptador de IChunkStore sobre Microsoft.Extensions.
/// VectorData: o código serve a qualquer provedor.</summary>
public sealed class VectorChunkStore : IChunkStore
{
    private readonly
        VectorStoreCollection<string, ChunkRecord> _col;
    private bool _ready;

    public VectorChunkStore(VectorStore store,
        int dimensions = ChunkRecord.Dimensions) =>
        _col = store.GetCollection<string, ChunkRecord>(
            "chunks", Definition(dimensions));

    /// <summary>Mapeamento por código: a dimensão do vetor é
    /// configuração (768 no nomic-embed-text, 1536 no
    /// text-embedding-3-small).</summary>
    public static VectorStoreCollectionDefinition Definition(
        int dims) => new()
    {
        Properties =
        [
            new VectorStoreKeyProperty("Id", typeof(string)),
            Data("DocumentId", typeof(string), true),
            Data("Index", typeof(int), true),
            Data("Access", typeof(int), true),
            Data("Title", typeof(string)),
            Data("Section", typeof(string)),
            Data("Version", typeof(string)),
            Data("Source", typeof(string)),
            Data("Area", typeof(string)),
            Data("Text", typeof(string)),
            Data("Hash", typeof(string)),
            Data("DocumentHash", typeof(string)),
            new VectorStoreVectorProperty("Embedding",
                typeof(ReadOnlyMemory<float>), dims)
            {
                DistanceFunction =
                    DistanceFunction.CosineSimilarity
            }
        ]
    };

    private static VectorStoreDataProperty Data(
        string name, Type type, bool indexed = false) =>
        new(name, type) { IsIndexed = indexed };

    private async Task<VectorStoreCollection<string, ChunkRecord>>
        ColAsync(CancellationToken ct)
    {
        if (!_ready)
        {
            await _col.EnsureCollectionExistsAsync(ct);
            _ready = true;
        }
        return _col;
    }

    public async Task<string?> GetDocumentHashAsync(
        string documentId, CancellationToken ct = default)
    {
        var col = await ColAsync(ct);
        await foreach (var r in col.GetAsync(
            x => x.DocumentId == documentId, 1, null, ct))
            return r.DocumentHash;
        return null;
    }

    public async Task<IReadOnlyList<string>> ListDocumentIdsAsync(
        CancellationToken ct = default)
    {
        var col = await ColAsync(ct);
        var ids = new List<string>();
        await foreach (var r in col.GetAsync(
            x => x.Index == 0, 10_000, null, ct))
            ids.Add(r.DocumentId);
        return ids;
    }

    public async Task ReplaceDocumentAsync(
        string documentId, IReadOnlyList<IndexedChunk> chunks,
        CancellationToken ct = default)
    {
        await DeleteDocumentAsync(documentId, ct);
        var col = await ColAsync(ct);
        await col.UpsertAsync(chunks.Select(ToRecord), ct);
    }

    public async Task DeleteDocumentAsync(
        string documentId, CancellationToken ct = default)
    {
        var col = await ColAsync(ct);
        var keys = new List<string>();
        await foreach (var r in col.GetAsync(
            x => x.DocumentId == documentId, 10_000, null, ct))
            keys.Add(r.Id);
        if (keys.Count > 0) await col.DeleteAsync(keys, ct);
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        ReadOnlyMemory<float> query, int top,
        AccessLevel maxAccess, CancellationToken ct = default)
    {
        var col = await ColAsync(ct);
        var level = (int)maxAccess;
        var options = new VectorSearchOptions<ChunkRecord>
        {
            // Filtro de acesso NO banco, antes do top-k.
            Filter = x => x.Access <= level,
            IncludeVectors = true
        };
        var found = new List<ScoredChunk>();
        await foreach (var r in col.SearchAsync(
            query, top, options, ct))
            found.Add(new(ToChunk(r.Record), r.Score ?? 0,
                r.Record.Embedding));
        return found;
    }

    private static ChunkRecord ToRecord(IndexedChunk i) => new()
    {
        Id = i.Chunk.Id, DocumentId = i.Chunk.DocumentId,
        Index = i.Chunk.Index, Access = (int)i.Chunk.Access,
        Title = i.Chunk.Title, Section = i.Chunk.Section,
        Version = i.Chunk.Version, Source = i.Chunk.Source,
        Area = i.Chunk.Area,
        Text = i.Chunk.Text, Hash = i.Chunk.Hash,
        DocumentHash = i.Chunk.DocumentHash, Embedding = i.Vector
    };

    private static Chunk ToChunk(ChunkRecord r) => new(
        r.Id, r.DocumentId, r.Index, r.Title, r.Section,
        r.Version, r.Source, (AccessLevel)r.Access, r.Text,
        r.Hash, r.DocumentHash, r.Area);
}
