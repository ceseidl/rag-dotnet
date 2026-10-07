namespace Rag.Application;

public sealed class RagOptions
{
    public const string Section = "Rag";

    // Ingestão
    public int ChunkSize { get; set; } = 800;
    public int ChunkOverlap { get; set; } = 120;
    public int EmbeddingBatchSize { get; set; } = 64;

    // Consulta
    public int Candidates { get; set; } = 16;
    public int TopK { get; set; } = 4;
    public double MinScore { get; set; } = 0.12;
    public double MmrLambda { get; set; } = 0.7;
    public int MaxContextChars { get; set; } = 6000;
    public int MaxQuestionLength { get; set; } = 500;
    public int MaxOutputTokens { get; set; } = 400;

    // Modelos de raciocínio (gpt-5-mini) rejeitam temperature e
    // gastam tokens de raciocínio dentro de MaxOutputTokens.
    public bool SendTemperature { get; set; } = true;
    public string ReasoningEffort { get; set; } = "";
}
