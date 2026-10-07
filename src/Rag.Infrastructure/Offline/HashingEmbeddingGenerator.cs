using Microsoft.Extensions.AI;
using Rag.Application;
using Rag.Infrastructure.Vectors;

namespace Rag.Infrastructure.Offline;

/// <summary>FAKE determinístico para testes e demo sem rede:
/// bag-of-words com hashing de termos. Valida a MECÂNICA do
/// pipeline; NÃO tem qualidade semântica real.</summary>
public sealed class HashingEmbeddingGenerator(
    int dimensions = ChunkRecord.Dimensions)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata { get; } =
        new("offline", defaultModelId: "hashing-bow-v1",
            defaultModelDimensions: dimensions);

    public Task<GeneratedEmbeddings<Embedding<float>>>
        GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>(
            values.Select(v =>
                new Embedding<float>(Embed(v, dimensions))));
        return Task.FromResult(result);
    }

    public static float[] Embed(
        string text, int dims = ChunkRecord.Dimensions)
    {
        var v = new float[dims];
        foreach (var g in TextTokens.Of(text).GroupBy(t => t))
        {
            var h = Fnv(g.Key);
            var sign = (h & 1) == 0 ? 1f : -1f;
            v[(int)(h >> 1) % v.Length] +=
                sign * (1f + MathF.Log(g.Count()));
        }
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm > 0)
            for (var i = 0; i < v.Length; i++) v[i] /= norm;
        return v;
    }

    private static uint Fnv(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) h = (h ^ c) * 16777619u;
        return h;
    }

    public object? GetService(Type type, object? key = null) =>
        key is not null ? null
        : type == typeof(EmbeddingGeneratorMetadata) ? Metadata
        : type.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
