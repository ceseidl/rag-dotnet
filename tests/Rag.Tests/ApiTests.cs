using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rag.Application.Abstractions;
using Rag.Tests.Support;

namespace Rag.Tests;

public class ApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient Client(string? user = "ana",
        string? level = null, bool admin = false,
        Action<IServiceCollection>? services = null)
    {
        var c = factory.WithWebHostBuilder(b =>
            {
                b.UseSetting("Ai:Mode", "Offline");
                b.ConfigureTestServices(s => services?.Invoke(s));
            })
            .CreateClient();
        if (user is not null) c.DefaultRequestHeaders.Add("X-Dev-User", user);
        if (level is not null)
            c.DefaultRequestHeaders.Add("X-Dev-Level", level);
        if (admin) c.DefaultRequestHeaders.Add("X-Dev-Admin", "true");
        return c;
    }

    private static Task<HttpResponseMessage> Ask(
        HttpClient c, string q) =>
        c.PostAsJsonAsync("/api/v1/ask", new { question = q });

    [Fact]
    public async Task Requires_authentication()
    {
        var r = await Ask(Client(user: null), "Posso fazer deploy?");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Validates_the_question()
    {
        var r = await Ask(Client(), "x");
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("Question",
            await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Answers_with_citations_and_reports_version()
    {
        var r = await Ask(Client(level: "Team"),
            "Posso fazer deploy na sexta-feira?");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("1", r.Headers.GetValues("api-supported-versions"));
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("answered").GetBoolean());
        Assert.Equal("politica-de-deploy", json.GetProperty("citations")[0]
            .GetProperty("documentId").GetString());
    }

    [Fact]
    public async Task Access_level_comes_from_the_token()
    {
        const string q = "Como uso o acesso break-glass ao banco?";
        var team = await (await Ask(Client(level: "Team"), q))
            .Content.ReadFromJsonAsync<JsonElement>();
        var boss = await (await Ask(Client(level: "Restricted"), q))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(team.GetProperty("answered").GetBoolean());
        Assert.True(boss.GetProperty("answered").GetBoolean());
    }

    [Fact]
    public async Task Provider_failure_becomes_503_with_retry_after()
    {
        var c = Client(level: "Team", services: s =>
        {
            s.RemoveAll<IChatClient>();
            s.AddSingleton<IChatClient>(new ThrowingChat());
        });

        var r = await Ask(c, "Posso fazer deploy na sexta-feira?");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
        Assert.Equal("30", r.Headers.GetValues("Retry-After").Single());
    }

    [Fact]
    public async Task Reindex_is_admin_only()
    {
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Client().PostAsync("/api/v1/admin/reindex", null))
            .StatusCode);
        var ok = await Client(admin: true)
            .PostAsync("/api/v1/admin/reindex", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    private sealed class ThrowingChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> m, ChatOptions? o = null,
            CancellationToken c = default) =>
            throw new ProviderUnavailableException(
                "429", TimeSpan.FromSeconds(30));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> m, ChatOptions? o = null,
            CancellationToken c = default) =>
            throw new NotSupportedException();

        public object? GetService(Type t, object? k = null) => null;
        public void Dispose() { }
    }
}
