namespace Rag.Domain;

/// <summary>Um trecho indexável, com os metadados.</summary>
public sealed record Chunk(
    string Id,
    string DocumentId,
    int Index,
    string Title,
    string Section,
    string Version,
    string Source,
    AccessLevel Access,
    string Text,
    string Hash,
    string DocumentHash)
{
    /// <summary>Texto que vira embedding: título e seção dão
    /// contexto a um trecho que sozinho seria ambíguo.</summary>
    public string EmbeddingInput =>
        $"{Title} > {Section}\n{Text}";
}
