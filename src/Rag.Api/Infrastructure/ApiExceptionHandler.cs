using Microsoft.AspNetCore.Diagnostics;
using Rag.Application.Abstractions;

namespace Rag.Api.Infrastructure;

/// <summary>429/queda do provedor de IA vira 503 com Retry-After,
/// sem expor o provedor nem a mensagem original. Corpo JSON
/// inválido vira 400.</summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is BadHttpRequestException bad)
        {
            // JSON inválido ou fora do formato: erro do cliente.
            ctx.Response.StatusCode = bad.StatusCode;
            await ctx.Response.WriteAsJsonAsync(new
            {
                title = "Requisição inválida",
                status = bad.StatusCode
            }, ct);
            return true;
        }
        if (ex is not ProviderUnavailableException p)
            return false;

        var after = p.RetryAfter ?? TimeSpan.FromSeconds(5);
        var wait = (int)Math.Ceiling(after.TotalSeconds);
        ctx.Response.StatusCode =
            StatusCodes.Status503ServiceUnavailable;
        ctx.Response.Headers.RetryAfter = wait.ToString();
        await ctx.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.io/503",
            title = "Serviço de IA temporariamente indisponível",
            status = 503,
            detail = $"Tente novamente em {wait} s."
        }, ct);
        return true;
    }
}
