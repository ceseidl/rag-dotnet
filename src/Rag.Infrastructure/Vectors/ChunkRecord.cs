using Microsoft.Extensions.VectorData;

namespace Rag.Infrastructure.Vectors;

/// <summary>Modelo de armazenamento. Fica na infraestrutura: o
/// domínio não conhece atributos de vector store.</summary>
public sealed class ChunkRecord
{
    // Dimensão do text-embedding-3-small (padrão); constante
    // porque o atributo exige um valor de compilação.
    public const int Dimensions = 1536;

    [VectorStoreKey]
    public string Id { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public string DocumentId { get; set; } = "";

    [VectorStoreData(IsIndexed = true)]
    public int Index { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public int Access { get; set; }

    [VectorStoreData] public string Title { get; set; } = "";
    [VectorStoreData] public string Section { get; set; } = "";
    [VectorStoreData] public string Version { get; set; } = "";
    [VectorStoreData] public string Source { get; set; } = "";
    [VectorStoreData] public string Text { get; set; } = "";
    [VectorStoreData] public string Hash { get; set; } = "";
    [VectorStoreData]
    public string DocumentHash { get; set; } = "";

    [VectorStoreVector(Dimensions,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
