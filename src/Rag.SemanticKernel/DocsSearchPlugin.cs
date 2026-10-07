using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Rag.Application.Querying;
using Rag.Domain;

namespace Rag.SemanticKernel;

/// <summary>A mesma recuperação, exposta como ferramenta para
/// agentes (Semantic Kernel ou Agent Framework). O nível de
/// acesso é fixado no construtor: o modelo não escolhe.</summary>
public sealed class DocsSearchPlugin(
    RetrievalService retrieval, AccessLevel access)
{
    [KernelFunction("search_docs")]
    [Description("Busca trechos na documentação do time.")]
    public async Task<string> SearchAsync(
        [Description("Pergunta ou termos de busca")] string query,
        CancellationToken ct = default)
    {
        var found = await retrieval
            .RetrieveAsync(query, access, ct);
        var guarded = ContextGuard.Apply(found.Selected, 4000);
        if (guarded.Chunks.Count == 0) return "Nada encontrado.";

        var sb = new StringBuilder();
        foreach (var s in guarded.Chunks)
            sb.AppendLine(
                $"[{s.Chunk.Source} v{s.Chunk.Version}] " +
                ContextGuard.Neutralize(s.Chunk.Text));
        return sb.ToString();
    }

    /// <summary>Plugin do Semantic Kernel.</summary>
    public KernelPlugin ToKernelPlugin() =>
        KernelPluginFactory.CreateFromObject(this, "docs");

    /// <summary>Ferramenta da Microsoft.Extensions.AI (vale para
    /// qualquer IChatClient com UseFunctionInvocation).</summary>
    public AIFunction ToAIFunction() =>
        AIFunctionFactory.Create(SearchAsync, "search_docs");
}
