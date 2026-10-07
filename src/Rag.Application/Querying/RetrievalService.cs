using Microsoft.Extensions.AI;
using Rag.Application.Abstractions;
using Rag.Domain;

namespace Rag.Application.Querying;

public sealed record Retrieval(
    int Candidates,
    int AboveThreshold,
    IReadOnlyList<ScoredChunk> Selected);

/// <summary>Etapa de RECUPERAÇÃO: embedding da pergunta, top-k
/// com filtro de acesso, limiar de similaridade e MMR.</summary>
public sealed class RetrievalService(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IChunkStore store,
    RagOptions options)
{
    public async Task<Retrieval> RetrieveAsync(
        string question, AccessLevel access, CancellationToken ct)
    {
        float[] vector;
        using (var s = RagTelemetry.Start("embed-query"))
        {
            // Normalizar aumenta o acerto do cache de embeddings.
            var key = string.Join(' ', question.ToLowerInvariant()
                .Split(' ',
                    StringSplitOptions.RemoveEmptyEntries));
            var e = await embedder.GenerateAsync(
                key, cancellationToken: ct);
            vector = e.Vector.ToArray();
        }

        using var stage = RagTelemetry.Start("retrieve");
        var found = await store.SearchAsync(
            vector, options.Candidates, access, ct);
        var passed = found
            .Where(c => c.Score >= options.MinScore).ToList();
        var picked = Mmr.Select(
            passed, options.TopK, options.MmrLambda);

        stage.Tag("rag.candidates", found.Count);
        stage.Tag("rag.selected", picked.Count);
        RagTelemetry.Chunks.Add(picked.Count);
        return new(found.Count, passed.Count, picked);
    }
}
