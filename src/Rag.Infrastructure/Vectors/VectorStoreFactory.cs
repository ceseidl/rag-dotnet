using Azure;
using Azure.Search.Documents.Indexes;
using CommunityToolkit.VectorData.AzureAISearch;
using CommunityToolkit.VectorData.InMemory;
using CommunityToolkit.VectorData.PgVector;
using CommunityToolkit.VectorData.Qdrant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using Qdrant.Client;

namespace Rag.Infrastructure.Vectors;

/// <summary>Único lugar que conhece o provedor. Trocar de store é
/// mudar "VectorStore:Provider"; o domínio não muda.</summary>
public static class VectorStoreFactory
{
    public static VectorStore Create(IConfiguration c) =>
        (c["VectorStore:Provider"] ?? "InMemory") switch
        {
            "InMemory" => new InMemoryVectorStore(),

            "Postgres" => new PostgresVectorStore(
                c["VectorStore:ConnectionString"]!),

            "Qdrant" => new QdrantVectorStore(
                new QdrantClient(
                    c["VectorStore:Host"]!,
                    apiKey: c["VectorStore:ApiKey"]),
                ownsClient: true),

            "AzureAISearch" => new AzureAISearchVectorStore(
                new SearchIndexClient(
                    new Uri(c["VectorStore:Endpoint"]!),
                    new AzureKeyCredential(
                        c["VectorStore:ApiKey"]!))),

            var p => throw new InvalidOperationException(
                $"VectorStore:Provider desconhecido: {p}")
        };
}
