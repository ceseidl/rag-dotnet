namespace Rag.Infrastructure.Vectors;

/// <summary>Modelo de armazenamento. Fica na infraestrutura: o
/// domínio não conhece tipos de provedor. O mapeamento para o
/// vector store está em VectorChunkStore.Definition.</summary>
public sealed class ChunkRecord
{
    /// <summary>Dimensão padrão (text-embedding-3-small); a real
    /// vem de Ai:EmbeddingDimensions.</summary>
    public const int Dimensions = 1536;

    public string Id { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public int Index { get; set; }
    public int Access { get; set; }
    public string Title { get; set; } = "";
    public string Section { get; set; } = "";
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public string Area { get; set; } = "";
    public string Text { get; set; } = "";
    public string Hash { get; set; } = "";
    public string DocumentHash { get; set; } = "";
    public ReadOnlyMemory<float> Embedding { get; set; }
}
