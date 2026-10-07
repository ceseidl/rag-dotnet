namespace Rag.Domain;

/// <summary>Um documento inteiro, como veio da fonte.</summary>
public sealed record SourceDocument(
    string Id,
    string Title,
    string Version,
    AccessLevel Access,
    string Source,
    string Content,
    string Area = "");
