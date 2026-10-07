using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Rag.Domain;

namespace Rag.Api.Security;

/// <summary>SÓ para desenvolvimento e testes: monta o usuário a
/// partir de cabeçalhos. Em produção troque por JwtBearer; o
/// resto da API lê só claims e não muda.</summary>
public sealed class DevHeaderHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string SchemeName = "DevHeader";

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                "X-Dev-User", out var user))
            return Task.FromResult(
                AuthenticateResult.NoResult());

        var level = Request.Headers["X-Dev-Level"].ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.ToString()),
            new(UserClaims.AccessLevel,
                string.IsNullOrEmpty(level) ? "Public" : level)
        };
        if (Request.Headers["X-Dev-Admin"] == "true")
            claims.Add(new(ClaimTypes.Role, "rag-admin"));

        var id = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(id), SchemeName);
        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}

public static class UserClaims
{
    public const string AccessLevel = "access_level";

    /// <summary>Nível lido do token, nunca do corpo. Sem claim
    /// válida, o mínimo (Public).</summary>
    public static Domain.AccessLevel Level(
        this ClaimsPrincipal user) =>
        Enum.TryParse<Domain.AccessLevel>(
            user.FindFirstValue(AccessLevel), true, out var l)
            ? l : Domain.AccessLevel.Public;
}
