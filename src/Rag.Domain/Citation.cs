namespace Rag.Domain;

/// <summary>Fonte citada na resposta ([n] no texto).</summary>
public sealed record Citation(
    int Number,
    string DocumentId,
    string Title,
    string Section,
    string Version,
    string Source,
    double Score);
