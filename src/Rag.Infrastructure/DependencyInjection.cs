using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using OpenAI;
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
        s.AddSingleton<IDocumentSource>(
            new MarkdownFolderSource(DocsPath(config)));
        s.AddSingleton<VectorStore>(
            _ => VectorStoreFactory.Create(config));
        s.AddSingleton<IChunkStore, VectorChunkStore>();

        // IA: provedor real ou fakes, mesmas interfaces.
        s.AddDistributedMemoryCache();
        if (ai.Mode == "Offline") AddOffline(s);
        else AddProvider(s, ai);

        // Casos de uso.
        s.AddSingleton<MarkdownChunker>();
        s.AddSingleton<IngestionService>();
        s.AddSingleton<RetrievalService>();
        s.AddSingleton<AskService>();
        s.AddSingleton<EvaluationRunner>();
        return s;
    }

    private static void AddOffline(IServiceCollection s)
    {
        s.AddEmbeddingGenerator(new HashingEmbeddingGenerator())
            .UseDistributedCache()
            .UseOpenTelemetry(sourceName: RagTelemetry.AiName);
        s.AddChatClient(new ExtractiveChatClient())
            .UseOpenTelemetry(sourceName: RagTelemetry.AiName);
    }

    private static void AddProvider(
        IServiceCollection s, AiOptions ai)
    {
        if (string.IsNullOrWhiteSpace(ai.ApiKey) ||
            string.IsNullOrWhiteSpace(ai.Endpoint))
            throw new InvalidOperationException(
                "Ai:Endpoint e Ai:ApiKey são obrigatórios " +
                "(user-secrets ou variável Ai__ApiKey).");

        // Retry com backoff + jitter, timeouts e circuit breaker.
        s.AddHttpClient("ai").AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = ai.MaxRetries;
            o.Retry.ShouldRetryAfterHeader = true; // respeita 429
            o.AttemptTimeout.Timeout =
                TimeSpan.FromSeconds(ai.AttemptTimeoutSeconds);
            o.TotalRequestTimeout.Timeout =
                TimeSpan.FromSeconds(ai.TotalTimeoutSeconds);
            o.CircuitBreaker.SamplingDuration =
                o.AttemptTimeout.Timeout * 2;
        });

        s.AddSingleton(sp => new OpenAIClient(
            new ApiKeyCredential(ai.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(ai.Endpoint),
                // O retry fica no handler HTTP; o do SDK sairia
                // multiplicando as tentativas.
                RetryPolicy = new ClientRetryPolicy(0),
                Transport = new HttpClientPipelineTransport(
                    sp.GetRequiredService<IHttpClientFactory>()
                        .CreateClient("ai"))
            }));

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
        var configured = c["Documents:Path"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory);
             d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "docs-exemplo");
            if (Directory.Exists(p)) return p;
        }
        return "docs-exemplo";
    }
}
