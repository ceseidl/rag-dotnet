using CommunityToolkit.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Rag.Application;
using Rag.Application.Abstractions;
using Rag.Application.Ingestion;
using Rag.Application.Querying;
using Rag.Domain;
using Rag.Infrastructure.Documents;
using Rag.Infrastructure.Offline;
using Rag.Infrastructure.Vectors;

namespace Rag.Tests.Support;

public sealed class ListSource(List<SourceDocument> docs)
    : IDocumentSource
{
    public List<SourceDocument> Docs { get; } = docs;

    public async IAsyncEnumerable<SourceDocument> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        foreach (var d in Docs) yield return d;
        await Task.CompletedTask;
    }
}

public sealed class SpyChat(string reply) : IChatClient
{
    public List<IList<ChatMessage>> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(messages.ToList());
        return Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, reply)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> m, ChatOptions? o = null,
        CancellationToken c = default) =>
        throw new NotSupportedException();

    public object? GetService(Type t, object? k = null) => null;
    public void Dispose() { }
}

public sealed class CountingEmbedder
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly HashingEmbeddingGenerator _inner = new();
    public List<int> BatchSizes { get; } = [];

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = values.ToList();
        BatchSizes.Add(list.Count);
        return _inner.GenerateAsync(list, options, cancellationToken);
    }

    public object? GetService(Type t, object? k = null) =>
        _inner.GetService(t, k);
    public void Dispose() { }
}

/// <summary>Monta o pipeline inteiro sem DI, com fakes offline.</summary>
public sealed class Rig
{
    public RagOptions Options { get; }
    public ListSource Source { get; }
    public IChunkStore Store { get; } =
        new VectorChunkStore(new InMemoryVectorStore());
    public IEmbeddingGenerator<string, Embedding<float>> Embedder { get; }
    public IChatClient Chat { get; }
    public IngestionService Ingestion { get; }
    public RetrievalService Retrieval { get; }
    public AskService Ask { get; }

    public Rig(IEnumerable<SourceDocument>? docs = null,
        IChatClient? chat = null,
        IEmbeddingGenerator<string, Embedding<float>>? embedder = null,
        RagOptions? options = null)
    {
        Options = options ?? new RagOptions();
        Source = new ListSource((docs ?? []).ToList());
        Embedder = embedder ?? new HashingEmbeddingGenerator();
        Chat = chat ?? new ExtractiveChatClient();
        Ingestion = new IngestionService(Source,
            new MarkdownChunker(Options), Embedder, Store, Options,
            NullLogger<IngestionService>.Instance);
        Retrieval = new RetrievalService(Embedder, Store, Options);
        Ask = new AskService(Retrieval, Chat, Options,
            NullLogger<AskService>.Instance);
    }

    public static string DocsFolder()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory);
             d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "docs-exemplo");
            if (Directory.Exists(p)) return p;
        }
        throw new DirectoryNotFoundException("docs-exemplo");
    }

    /// <summary>Rig com os documentos de exemplo do repositório.</summary>
    public static async Task<Rig> WithSampleDocsAsync(
        IChatClient? chat = null,
        IEmbeddingGenerator<string, Embedding<float>>? embedder = null)
    {
        var docs = new List<SourceDocument>();
        await foreach (var d in new MarkdownFolderSource(DocsFolder())
                           .ReadAllAsync())
            docs.Add(d);
        var rig = new Rig(docs, chat, embedder);
        await rig.Ingestion.RunAsync();
        return rig;
    }

    public static SourceDocument Doc(string id, string text,
        AccessLevel access = AccessLevel.Team, string version = "1") =>
        new(id, id, version, access, id + ".md", text);
}
