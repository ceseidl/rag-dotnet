[English](README.md) | Português

# rag-dotnet

Repositório do artigo "RAG em .NET: Respostas Baseadas nos Seus
Dados". Uma solução .NET 10 em camadas que responde perguntas sobre
a documentação interna de um time (runbooks, política de deploy,
ADRs, FAQ de onboarding), cita as fontes e diz "não sei" quando os
documentos não cobrem a pergunta.

Usa `Microsoft.Extensions.AI` (`IChatClient`,
`IEmbeddingGenerator`) e `Microsoft.Extensions.VectorData` (vector
stores). O Semantic Kernel entra como adaptador
(`Rag.SemanticKernel`).

## Importante: situação do provedor

O artigo foi planejado em torno do GitHub Models. O GitHub
aposentou esse serviço em **30/07/2026** (playground, catálogo e API
de inferência). O código não depende dele: o provedor é qualquer
endpoint compatível com a API OpenAI (Microsoft Foundry / Azure OpenAI
v1, OpenAI, servidor local), escolhido por configuração.

## Requisitos

- [SDK do .NET 10](https://dotnet.microsoft.com/download)
- Mais nada para compilar e testar: sem rede, sem token.

## Build e testes (offline)

```bash
dotnet build
dotnet test
```

Esperado: 35 aprovados, 0 falhas. Os testes usam fakes determinísticos
atrás das mesmas interfaces (`HashingEmbeddingGenerator`, bag-of-words
com hashing; `ExtractiveChatClient`, que escolhe uma frase do
contexto). Eles validam a **mecânica** do pipeline (chunking,
reindexação, limiar, filtro de acesso, proteção contra injeção,
citações, retentativas). As métricas que reportam **não** medem a
qualidade semântica de um modelo real.

## Rodar a API (modo offline, padrão)

```bash
cd src/Rag.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

O esquema de autenticação `DevHeader` (só desenvolvimento) monta o
usuário a partir de cabeçalhos. O nível de acesso vem do token, nunca
do corpo.

```bash
curl -X POST http://localhost:5025/api/v1/ask \
  -H "Content-Type: application/json" \
  -H "X-Dev-User: ana" -H "X-Dev-Level: Team" \
  -d '{"question":"Posso fazer deploy na sexta-feira?"}'
```

| Endpoint | Descrição |
| -------- | --------- |
| `POST /api/v1/ask` | pergunta entra, resposta + citações saem |
| `POST /api/v1/admin/reindex` | reindexa (papel `rag-admin`) |
| `GET /health` | health check |

## Avaliação

```bash
cd src/Rag.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run -- --eval
```

Indexa os documentos e roda `eval/golden.json` (hit rate, MRR,
recall@k, abstenção correta, ancoragem). Com os fakes valida o
encanamento; rode de novo com um provedor real para medir a qualidade
e calibrar `Rag:MinScore`.

## Modo real (não executado pelo autor)

Segredos só por user-secrets ou variáveis de ambiente:

```bash
cd src/Rag.Api
dotnet user-secrets set "Ai:Mode" "OpenAiCompatible"
dotnet user-secrets set "Ai:Endpoint" "https://<recurso>.openai.azure.com/openai/v1/"
dotnet user-secrets set "Ai:ApiKey" "<chave>"
dotnet user-secrets set "Ai:ChatModel" "<deployment de chat>"
dotnet user-secrets set "Ai:EmbeddingModel" "<deployment de embedding>"
dotnet user-secrets set "Rag:MinScore" "0.30"
```

As variáveis de ambiente equivalentes usam `__` (por exemplo
`Ai__ApiKey`). O modelo de embedding precisa gerar 1536 dimensões
(`text-embedding-3-small` gera); para outro tamanho, altere
`ChunkRecord.Dimensions`. O formato do `Endpoint` segue a
documentação v1 do Microsoft Foundry; este repositório não chamou um
provedor real.

## Trocar o vector store

`appsettings.json` (ou variáveis de ambiente):

```json
{ "VectorStore": { "Provider": "Postgres",
                   "ConnectionString": "Host=...;Database=rag" } }
```

Provedores no `VectorStoreFactory`: `InMemory`, `Postgres` (pgvector),
`Qdrant` (`Host`, `ApiKey`) e `AzureAISearch` (`Endpoint`, `ApiKey`).
O código compila contra todos; só o `InMemory` foi executado.

## Estrutura

```
src/
  Rag.Domain/          Chunk, SourceDocument, Citation, AccessLevel
  Rag.Application/     ingestão, recuperação, prompt, guardrails,
                       avaliação, telemetria (portas: IChunkStore,
                       IDocumentSource)
  Rag.Infrastructure/  store VectorData, cliente compatível com
                       OpenAI, resiliência, fakes offline, DI
  Rag.SemanticKernel/  recuperação como plugin do Semantic Kernel e
                       como ferramenta da Microsoft.Extensions.AI
  Rag.Api/             Minimal API: versionamento, validação,
                       autenticação, rate limit, OpenTelemetry
tests/Rag.Tests/       xUnit, sem rede
docs-exemplo/          documentos de exemplo
eval/golden.json       conjunto de perguntas-ouro
```

## Notas

- O `MinScore` offline é 0,12 porque as notas do bag-of-words são
  menores que as de embeddings neurais; com um modelo real comece
  perto de 0,30 e calibre com o conjunto-ouro.
- O store InMemory é para desenvolvimento. Rode testes de integração
  contra o store de produção (Testcontainers) antes de confiar nele.
