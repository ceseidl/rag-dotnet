English | [Português](README.pt-BR.md)

# rag-dotnet

Companion repository for the article "RAG in .NET: Answers Based on
Your Own Data". A layered .NET 10 solution that answers questions
about a team's internal documentation (runbooks, deploy policy,
ADRs, onboarding FAQ), citing the sources, and says "I don't know"
when the documents do not cover the question.

Built on `Microsoft.Extensions.AI` (`IChatClient`,
`IEmbeddingGenerator`) and `Microsoft.Extensions.VectorData`
(vector stores). Semantic Kernel plugs in as an adapter
(`Rag.SemanticKernel`).

## Important: provider status

The article was planned around GitHub Models. GitHub retired that
service on **July 30, 2026** (playground, catalog and inference API).
The code does not depend on it: the provider is any endpoint
compatible with the OpenAI API (Microsoft Foundry / Azure OpenAI v1,
OpenAI, a local server), chosen by configuration.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Nothing else to build and test: no network, no token.

## Build and test (offline)

```bash
dotnet build
dotnet test
```

Expected: 35 passed, 0 failed. The tests use deterministic fakes
behind the same interfaces (`HashingEmbeddingGenerator`, a
bag-of-words hashing; `ExtractiveChatClient`, which picks a sentence
from the context). They validate the **mechanics** of the pipeline
(chunking, re-indexing, thresholds, access filter, injection guard,
citations, retries). The metrics they report do **not** measure the
semantic quality of a real model.

## Run the API (offline mode, default)

```bash
cd src/Rag.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

The `DevHeader` authentication scheme (development only) builds the
user from headers. The access level comes from the token, never from
the body.

```bash
curl -X POST http://localhost:5025/api/v1/ask \
  -H "Content-Type: application/json" \
  -H "X-Dev-User: ana" -H "X-Dev-Level: Team" \
  -d '{"question":"Can I deploy on Friday?"}'
```

The sample documents are in Portuguese (`docs-exemplo/`), so ask in
Portuguese: `Posso fazer deploy na sexta-feira?`.

| Endpoint | Description |
| -------- | ----------- |
| `POST /api/v1/ask` | question in, answer + citations out |
| `POST /api/v1/admin/reindex` | re-index (role `rag-admin`) |
| `GET /health` | health check |

## Evaluation

```bash
cd src/Rag.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run -- --eval
```

Indexes the documents and runs `eval/golden.json` (hit rate, MRR,
recall@k, correct abstention, grounding). With the fakes it validates
the plumbing; run it again with a real provider to measure quality
and calibrate `Rag:MinScore`.

## Real mode (not executed by the author)

Secrets only through user-secrets or environment variables:

```bash
cd src/Rag.Api
dotnet user-secrets set "Ai:Mode" "OpenAiCompatible"
dotnet user-secrets set "Ai:Endpoint" "https://<resource>.openai.azure.com/openai/v1/"
dotnet user-secrets set "Ai:ApiKey" "<key>"
dotnet user-secrets set "Ai:ChatModel" "<chat deployment>"
dotnet user-secrets set "Ai:EmbeddingModel" "<embedding deployment>"
dotnet user-secrets set "Rag:MinScore" "0.30"
```

Equivalent environment variables use `__` (for example
`Ai__ApiKey`). The embedding model must produce 1536 dimensions
(`text-embedding-3-small` does); to use another size, change
`ChunkRecord.Dimensions`. The `Endpoint` format follows the Microsoft
Foundry v1 documentation; this repository did not call a real
provider.

## Swap the vector store

`appsettings.json` (or environment variables):

```json
{ "VectorStore": { "Provider": "Postgres",
                   "ConnectionString": "Host=...;Database=rag" } }
```

Providers in `VectorStoreFactory`: `InMemory`, `Postgres`
(pgvector), `Qdrant` (`Host`, `ApiKey`) and `AzureAISearch`
(`Endpoint`, `ApiKey`). The code compiles against all of them; only
`InMemory` was executed.

## Structure

```
src/
  Rag.Domain/          Chunk, SourceDocument, Citation, AccessLevel
  Rag.Application/     ingestion, retrieval, prompt, guardrails,
                       evaluation, telemetry (ports: IChunkStore,
                       IDocumentSource)
  Rag.Infrastructure/  VectorData store, OpenAI-compatible client,
                       resilience, offline fakes, DI
  Rag.SemanticKernel/  retrieval as a Semantic Kernel plugin and
                       as a Microsoft.Extensions.AI tool
  Rag.Api/             Minimal API: versioning, validation, auth,
                       rate limit, OpenTelemetry
tests/Rag.Tests/       xUnit, no network
docs-exemplo/          sample documents (pt-BR)
eval/golden.json       golden question set
```

## Notes

- Offline `MinScore` is 0.12 because bag-of-words scores are lower
  than neural embeddings; with a real model start around 0.30 and
  calibrate with the golden set.
- The InMemory store is for development. Run integration tests
  against your production store (Testcontainers) before trusting it.
