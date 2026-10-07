using Rag.Domain;

namespace Rag.Application.Abstractions;

/// <summary>De onde vêm os documentos (pasta, wiki...).</summary>
public interface IDocumentSource
{
    IAsyncEnumerable<SourceDocument> ReadAllAsync(
        CancellationToken ct = default);
}
