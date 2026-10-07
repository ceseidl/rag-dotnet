using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Rag.Application;
using Rag.Application.Abstractions;
using Rag.Application.Querying;
using Rag.Tests.Support;
using Rag.Infrastructure;

namespace Rag.Tests;

/// <summary>Modo "real" sem rede: um handler HTTP falso devolve 429
/// e 200 no lugar do provedor. Testa o encanamento (retry, mapeamento
/// de erro), NÃO o provedor de verdade.</summary>
public class ProviderResilienceTests
{
    private sealed class StubHandler(
        Func<int, HttpResponseMessage> reply,
        Action<HttpRequestMessage>? inspect = null)
        : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            inspect?.Invoke(request);
            return Task.FromResult(
                reply(Interlocked.Increment(ref Calls)));
        }
    }

    private static HttpResponseMessage TooMany() =>
        new(HttpStatusCode.TooManyRequests)
        {
            Headers = { { "Retry-After", "0" } },
            Content = new StringContent("{\"error\":{\"message\":\"x\"}}")
        };

    private static HttpResponseMessage Embeddings()
    {
        var v = string.Join(',', Enumerable.Repeat("0.1", 1536));
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"object\":\"list\",\"model\":\"m\",\"data\":[" +
                "{\"object\":\"embedding\",\"index\":0,\"embedding\":[" +
                v + "]}],\"usage\":{\"prompt_tokens\":1," +
                "\"total_tokens\":1}}", System.Text.Encoding.UTF8,
                "application/json")
        };
    }

    private static ServiceProvider Build(StubHandler stub)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Mode"] = "OpenAiCompatible",
                ["Ai:Endpoint"] = "https://provedor.invalido/v1",
                ["Ai:ApiKey"] = "token-de-teste",
                ["Ai:MaxRetries"] = "2"
            }).Build();
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddRag(config);
        s.AddHttpClient("ai")
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        return s.BuildServiceProvider();
    }

    [Fact]
    public async Task Retries_a_429_and_then_succeeds()
    {
        var stub = new StubHandler(n => n == 1 ? TooMany() : Embeddings());
        using var sp = Build(stub);
        var embedder = sp.GetRequiredService<
            IEmbeddingGenerator<string, Embedding<float>>>();

        var e = await embedder.GenerateAsync("olá");

        Assert.Equal(1536, e.Vector.Length);
        Assert.Equal(2, stub.Calls);
    }

    [Fact]
    public async Task Persistent_429_becomes_provider_unavailable()
    {
        var stub = new StubHandler(_ => TooMany());
        using var sp = Build(stub);
        var embedder = sp.GetRequiredService<
            IEmbeddingGenerator<string, Embedding<float>>>();

        var ex = await Assert.ThrowsAsync<ProviderUnavailableException>(
            () => embedder.GenerateAsync("olá"));

        Assert.Equal(3, stub.Calls);          // 1 + 2 retentativas
        Assert.Equal(TimeSpan.Zero, ex.RetryAfter);
    }

    [Fact]
    public async Task Reasoning_model_request_has_only_accepted_parameters()
    {
        string? body = null;
        var stub = new StubHandler(_ => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"id\":\"x\",\"object\":\"chat.completion\"," +
                "\"created\":1,\"model\":\"gpt-5-mini\",\"choices\":" +
                "[{\"index\":0,\"finish_reason\":\"stop\",\"message\":" +
                "{\"role\":\"assistant\",\"content\":\"Sim. [1]\"}}]," +
                "\"usage\":{\"prompt_tokens\":9,\"completion_tokens\":3," +
                "\"total_tokens\":12}}", System.Text.Encoding.UTF8,
                "application/json")
        }, r => body = r.Content!.ReadAsStringAsync().Result);
        using var sp = Build(stub);
        var rig = await Rig.WithSampleDocsAsync();
        var opt = new RagOptions
        {
            MaxOutputTokens = 2000,
            SendTemperature = false,
            ReasoningEffort = "Low"
        };
        var ask = new AskService(rig.Retrieval,
            sp.GetRequiredService<IChatClient>(), opt,
            NullLogger<AskService>.Instance);

        await ask.AskAsync(new("Posso fazer deploy na sexta-feira?",
            Rag.Domain.AccessLevel.Team));

        Assert.Contains("\"max_completion_tokens\":2000", body);
        Assert.Contains("\"reasoning_effort\":\"low\"", body);
        Assert.DoesNotContain("temperature", body);
        Assert.DoesNotContain("\"max_tokens\"", body);
    }

    [Fact]
    public void No_provider_configured_is_a_clear_error()
    {
        var config = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddRag(config));

        Assert.Contains("docker start ollama", ex.Message);
        Assert.Contains("--offline", ex.Message);
    }

    [Fact]
    public void Real_mode_requires_endpoint_and_key()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                { ["Ai:Mode"] = "OpenAiCompatible" }).Build();

        Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddRag(config));
    }
}
