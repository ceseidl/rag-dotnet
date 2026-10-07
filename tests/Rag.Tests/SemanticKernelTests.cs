using Microsoft.SemanticKernel;
using Rag.Domain;
using Rag.SemanticKernel;
using Rag.Tests.Support;

namespace Rag.Tests;

public class SemanticKernelTests
{
    [Fact]
    public async Task Retrieval_is_exposed_as_a_kernel_function()
    {
        var rig = await Rig.WithSampleDocsAsync();
        var plugin = new DocsSearchPlugin(rig.Retrieval, AccessLevel.Team);
        var kernel = Kernel.CreateBuilder().Build();
        kernel.Plugins.Add(plugin.ToKernelPlugin());

        var result = await kernel.InvokeAsync("docs", "search_docs",
            new() { ["query"] = "Posso fazer deploy na sexta-feira?" });

        Assert.Contains("politica-de-deploy.md v2.3",
            result.ToString());
    }

    [Fact]
    public async Task Tool_respects_the_access_level_fixed_in_code()
    {
        var rig = await Rig.WithSampleDocsAsync();
        var plugin = new DocsSearchPlugin(rig.Retrieval, AccessLevel.Team);

        var text = await plugin.SearchAsync(
            "acesso break-glass ao banco de produção");

        Assert.DoesNotContain("runbook-incidente-sev1", text);
    }

    [Fact]
    public async Task Same_tool_as_ai_function()
    {
        var rig = await Rig.WithSampleDocsAsync();
        var fn = new DocsSearchPlugin(rig.Retrieval, AccessLevel.Team)
            .ToAIFunction();

        var result = await fn.InvokeAsync(new()
            { ["query"] = "Posso fazer deploy na sexta-feira?" });

        Assert.Contains("politica-de-deploy", result!.ToString());
    }
}
