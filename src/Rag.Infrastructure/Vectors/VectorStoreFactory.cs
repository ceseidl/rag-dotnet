using CommunityToolkit.VectorData.InMemory;
using CommunityToolkit.VectorData.PgVector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;

namespace Rag.Infrastructure.Vectors;

/// <summary>Único lugar que conhece o provedor. Trocar de store é
/// mudar "VectorStore:Provider"; o domínio não muda. Outros
/// provedores (Qdrant, Azure AI Search) seguem o mesmo molde:
/// um pacote CommunityToolkit.VectorData.* e um case.</summary>
public static class VectorStoreFactory
{
    public static VectorStore Create(IConfiguration c) =>
        (c["VectorStore:Provider"] ?? "InMemory") switch
        {
            "InMemory" => new InMemoryVectorStore(),

            "Postgres" => new PostgresVectorStore(
                c["VectorStore:ConnectionString"]!),

            var p => throw new InvalidOperationException(
                $"VectorStore:Provider desconhecido: {p}")
        };
}
