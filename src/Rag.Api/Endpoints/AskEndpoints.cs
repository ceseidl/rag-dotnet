using System.Security.Claims;
using Asp.Versioning;
using Rag.Api.Contracts;
using Rag.Api.Security;
using Rag.Application.Ingestion;
using Rag.Application.Querying;

namespace Rag.Api.Endpoints;

public static class AskEndpoints
{
    public static void MapRagEndpoints(this WebApplication app)
    {
        var v1 = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions().Build();

        var api = app.MapGroup("/api/v{version:apiVersion}")
            .WithApiVersionSet(v1).RequireAuthorization();

        api.MapPost("/ask", AskAsync).RequireRateLimiting("ask");
        api.MapPost("/admin/reindex", ReindexAsync)
            .RequireAuthorization("RagAdmin");

        app.MapHealthChecks("/health").AllowAnonymous();
    }

    private static async Task<IResult> AskAsync(
        AskBody body, ClaimsPrincipal user,
        AskService ask, CancellationToken ct)
    {
        // O nível de acesso vem do token; nunca do corpo.
        var result = await ask.AskAsync(
            new(body.Question.Trim(), user.Level()), ct);
        return TypedResults.Ok(AskResponse.From(result));
    }

    private static async Task<IResult> ReindexAsync(
        IngestionService ingestion, CancellationToken ct) =>
        TypedResults.Ok(await ingestion.RunAsync(ct));
}
