using Rag.Domain;

namespace Rag.Application.Abstractions;

public sealed record IndexedChunk(
    Chunk Chunk, ReadOnlyMemory<float> Vector);

/// <summary>Score: quanto MAIOR, mais parecido.</summary>
public sealed record ScoredChunk(
    Chunk Chunk, double Score, ReadOnlyMemory<float> Vector);

/// <summary>Porta do índice vetorial. O domínio só conhece isto;
/// o provedor (InMemory, pgvector...) fica na infra.</summary>
public interface IChunkStore
{
    Task<string?> GetDocumentHashAsync(
        string documentId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListDocumentIdsAsync(
        CancellationToken ct = default);

    Task ReplaceDocumentAsync(
        string documentId,
        IReadOnlyList<IndexedChunk> chunks,
        CancellationToken ct = default);

    Task DeleteDocumentAsync(
        string documentId, CancellationToken ct = default);

    Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        ReadOnlyMemory<float> query,
        int top,
        AccessLevel maxAccess,
        CancellationToken ct = default);
}
