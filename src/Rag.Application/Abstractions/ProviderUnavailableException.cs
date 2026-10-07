namespace Rag.Application.Abstractions;

/// <summary>O provedor de IA recusou (429) ou caiu. A API traduz
/// para 503 com Retry-After, sem vazar o provedor.</summary>
public sealed class ProviderUnavailableException(
    string message, TimeSpan? retryAfter, Exception? inner = null)
    : Exception(message, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
