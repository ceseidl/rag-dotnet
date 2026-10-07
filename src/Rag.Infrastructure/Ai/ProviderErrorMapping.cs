using System.ClientModel;
using Microsoft.Extensions.AI;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Rag.Application.Abstractions;

namespace Rag.Infrastructure.Ai;

/// <summary>Traduz falhas do provedor (429, 5xx, timeout,
/// circuito aberto) para um tipo da aplicação.</summary>
public static class ProviderErrors
{
    public static Exception Map(Exception ex) => ex switch
    {
        ClientResultException { Status: 429 or >= 500 } c =>
            new ProviderUnavailableException(
                $"Provedor de IA respondeu {c.Status}.",
                RetryAfter(c), c),
        HttpRequestException or TimeoutRejectedException
            or BrokenCircuitException =>
            new ProviderUnavailableException(
                "Provedor de IA indisponível.", null, ex),
        _ => ex
    };

    private static TimeSpan? RetryAfter(
        ClientResultException c) =>
        c.GetRawResponse()?.Headers.TryGetValue(
            "Retry-After", out var v) == true
        && int.TryParse(v, out var s)
            ? TimeSpan.FromSeconds(s) : null;
}

public sealed class ErrorMappingChatClient(IChatClient inner)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(
                messages, options, cancellationToken);
        }
        catch (Exception ex) when (ProviderErrors.Map(ex) != ex)
        {
            throw ProviderErrors.Map(ex);
        }
    }
}

public sealed class ErrorMappingEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> inner)
    : DelegatingEmbeddingGenerator<string, Embedding<float>>(
        inner)
{
    public override async
        Task<GeneratedEmbeddings<Embedding<float>>>
        GenerateAsync(IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GenerateAsync(
                values, options, cancellationToken);
        }
        catch (Exception ex) when (ProviderErrors.Map(ex) != ex)
        {
            throw ProviderErrors.Map(ex);
        }
    }
}
