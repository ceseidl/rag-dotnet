English | [Português](README.pt-BR.md)

# rag-dotnet

Companion repository for the article "RAG in .NET: Answers Based on
Your Own Data, Step by Step". A small layered .NET 10 solution that
answers questions about a (fictional) software team's documentation
(runbooks, deploy policy, ADRs, onboarding FAQ), cites its sources
and says "I don't know" when the documents do not cover the question.

Built on `Microsoft.Extensions.AI` (`IChatClient`,
`IEmbeddingGenerator`) and `Microsoft.Extensions.VectorData`. Semantic
Kernel plugs in as an adapter (`Rag.SemanticKernel`).

**It runs 100% local, with no account and no cost** (Ollama), and the
same code runs against Azure AI Foundry by changing configuration only.

> GitHub Models, the provider the article was first planned around,
> was retired by GitHub on July 30, 2026. The provider is just
> configuration here.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker, only for the real local model (about 2.2 GB of models)

## 1. Build and test (offline, no network, no token)

```bash
dotnet build
dotnet test
```

Expected: 33 passed. The tests use deterministic fakes behind the same
interfaces; they validate the **mechanics** (chunking, re-indexing,
thresholds, access filter, injection guard, citations, retries), not
the semantic quality of a model.

## 2. Option A: Ollama (local, executed by the author)

```bash
docker run -d -v ollama:/root/.ollama -p 11434:11434 \
  --name ollama ollama/ollama
docker exec ollama ollama pull nomic-embed-text   # embeddings, 768 dims
docker exec ollama ollama pull qwen2.5:3b         # chat, 3B, CPU is ok
```

```bash
cd src/Rag.Api
Profile=Ollama ASPNETCORE_ENVIRONMENT=Development \
  dotnet run -- --ask "Quem aprova um hotfix fora da janela de deploy?"
Profile=Ollama ASPNETCORE_ENVIRONMENT=Development \
  dotnet run -- --eval
```

Measured by the author (CPU only, no GPU, 16 golden questions):
hit rate 100%, MRR 1.00, correct abstention 3/3, answered 10/13
in-scope questions (77%), mean 25.2 s per question. It is a small local
model: quality is lower than a large hosted model. `appsettings.Ollama.json`
holds the endpoint (`http://localhost:11434/v1`), the models, the vector
size (768) and `MinScore`.

Stop and clean up:

```bash
docker stop ollama && docker rm ollama
docker volume rm ollama        # deletes the downloaded models
```

## 3. Option B: Azure AI Foundry (from the docs, NOT executed by the author)

Create a Foundry resource and project, deploy one chat and one
embeddings model, and keep the endpoint and key out of the code:

```bash
cd src/Rag.Api
dotnet user-secrets set "Ai:ApiKey" "<your-key>"
dotnet user-secrets set "Ai:Endpoint" \
  "https://<resource>.openai.azure.com/openai/v1/"
```

Put your deployment names in `appsettings.Foundry.json` (no secrets
there) and run with `Profile=Foundry`. Without a key, use Microsoft
Entra ID: `Ai__Auth=EntraId` (`DefaultAzureCredential`; only the
client creation in `OpenAiClientFactory` changes). Step by step, with
the official documentation links, in the article. Check the official
pricing page: this option is pay-per-use.

## 4. Use your own data

Put `.md` or `.txt` files in `docs-meus/` (ignored by git) and run:

```bash
Documents__Path=docs-meus dotnet run --project src/Rag.Api
```

Per-document metadata goes in a frontmatter (`title`, `version`,
`area`, `access`: public | team | restricted). For your own golden
questions create `eval/meu-golden.json` (ignored by git) and set
`Eval__GoldenSet=eval/meu-golden.json` with `-- --eval`. PDF, Word and
HTML are not read: write another `IDocumentSource`. Do not index
secrets or raw personal data.

## API (offline mode by default)

```bash
cd src/Rag.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
curl -X POST http://localhost:5025/api/v1/ask \
  -H "Content-Type: application/json" \
  -H "X-Dev-User: ana" -H "X-Dev-Level: Team" \
  -d '{"question":"Posso fazer deploy na sexta-feira?"}'
```

`DevHeader` authentication is for development only (the access level
comes from the token, never from the body). `POST /api/v1/admin/reindex`
needs the `rag-admin` role (`X-Dev-Admin: true`). The sample documents
are in Portuguese, so ask in Portuguese.

## Swap the vector store

`VectorStore:Provider` = `InMemory` (default) or `Postgres` (pgvector,
with `VectorStore:ConnectionString`). Other providers follow the same
mold: a `CommunityToolkit.VectorData.*` package and one `case` in
`VectorStoreFactory`. Only InMemory was executed.

## Structure

```
src/
  Rag.Domain/          Chunk, SourceDocument, Citation, AccessLevel
  Rag.Application/     ingestion, retrieval, prompt, guardrails,
                       evaluation (ports: IChunkStore, IDocumentSource)
  Rag.Infrastructure/  VectorData store, OpenAI-compatible client,
                       resilience, offline fakes, DI
  Rag.SemanticKernel/  retrieval as Semantic Kernel plugin / AIFunction
  Rag.Api/             Minimal API, --ask and --eval commands
tests/Rag.Tests/       xUnit, no network
docs-exemplo/          fictional sample documents
docs-meus/             your documents (git-ignored)
eval/golden.json       golden question set
```
