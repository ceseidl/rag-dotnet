using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using OpenAI;
using Polly;
using Rag.Application;
using Rag.Application.Abstractions;
using Rag.Application.Evaluation;
using Rag.Application.Ingestion;
using Rag.Application.Querying;
using Rag.Infrastructure.Ai;
using Rag.Infrastructure.Documents;
using Rag.Infrastructure.Offline;
using Rag.Infrastructure.Vectors;

namespace Rag.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRag(
        this IServiceCollection s, IConfiguration config)
    {
        var rag = config.GetSection(RagOptions.Section)
            .Get<RagOptions>() ?? new();
        var ai = config.GetSection(AiOptions.Section)
            .Get<AiOptions>() ?? new();
        s.AddSingleton(rag);
        s.AddSingleton(ai);

        // Dados: documentos e índice (trocável por config).
        var ext = config.GetSection("Documents:Extensions")
            .Get<string[]>();
        s.AddSingleton<IDocumentSource>(new FolderDocumentSource(
            DocsPath(config), ext));
        s.AddSingleton<VectorStore>(
            _ => VectorStoreFactory.Create(config));
        s.AddSingleton<IChunkStore>(sp => new VectorChunkStore(
            sp.GetRequiredService<VectorStore>(),
            ai.EmbeddingDimensions));

        // IA: provedor real ou fakes, mesmas interfaces.
        s.AddDistributedMemoryCache();
        if (ai.Mode == "Offline") AddOffline(s, ai);
        else AddProvider(s, ai);

        // Casos de uso.
        s.AddSingleton<MarkdownChunker>();
        s.AddSingleton<IngestionService>();
        s.AddSingleton<RetrievalService>();
        s.AddSingleton<AskService>();
        s.AddSingleton<EvaluationRunner>();
        return s;
    }

    private static void AddOffline(
        IServiceCollection s, AiOptions ai)
    {
        s.AddEmbeddingGenerator(new HashingEmbeddingGenerator(
                ai.EmbeddingDimensions))
            .UseDistributedCache()
            .UseOpenTelemetry(sourceName: RagTelemetry.AiName);
        s.AddChatClient(new ExtractiveChatClient())
            .UseOpenTelemetry(sourceName: RagTelemetry.AiName);
    }

    private static void AddProvider(
        IServiceCollection s, AiOptions ai)
    {
        if (string.IsNullOrWhiteSpace(ai.Endpoint) ||
            (ai.Auth == "ApiKey" &&
             string.IsNullOrWhiteSpace(ai.ApiKey)))
            throw new InvalidOperationException(
                "Ai:Endpoint e Ai:ApiKey são obrigatórios " +
                "(user-secrets ou variável Ai__ApiKey).");

        // Retry com backoff + jitter, timeouts e circuit breaker.
        s.AddHttpClient("ai").AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = ai.MaxRetries;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
            o.Retry.UseJitter = true;
            o.Retry.ShouldRetryAfterHeader = true; // padrão
            o.AttemptTimeout.Timeout =
                TimeSpan.FromSeconds(ai.AttemptTimeoutSeconds);
            o.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(ai.TotalTimeoutSeconds);
            o.CircuitBreaker.SamplingDuration =
                o.AttemptTimeout.Timeout * 2;
        });

        s.AddSingleton(sp => OpenAiClientFactory.Create(ai,
            sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient("ai")));

        s.AddEmbeddingGenerator(sp => sp
                .GetRequiredService<OpenAIClient>()
                .GetEmbeddingClient(ai.EmbeddingModel)
                .AsIEmbeddingGenerator())
            .Use(g => new ErrorMappingEmbeddingGenerator(g))
            .UseDistributedCache()
            .UseOpenTelemetry(sourceName: RagTelemetry.AiName);

        s.AddChatClient(sp => sp
                .GetRequiredService<OpenAIClient>()
                .GetChatClient(ai.ChatModel)
                .AsIChatClient())
            .Use(c => new ErrorMappingChatClient(c))
            .UseOpenTelemetry(sourceName: RagTelemetry.AiName);
    }

    /// <summary>Procura a pasta de documentos subindo a partir do
    /// binário (ou usa Documents:Path).</summary>
    private static string DocsPath(IConfiguration c)
    {
        var name = c["Documents:Path"] ?? "docs-exemplo";
        if (Path.IsPathRooted(name)) return name;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory);
             d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, name);
            if (Directory.Exists(p)) return p;
        }
        return name;
    }
}
