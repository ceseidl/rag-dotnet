[English](README.md) | Português

# rag-dotnet

Repositório do artigo "RAG em .NET: Respostas Baseadas nos Seus Dados,
Passo a Passo". Uma solução .NET 10 enxuta, em camadas, que responde
perguntas sobre a documentação de um time de software fictício
(runbooks, política de deploy, ADRs, FAQ de onboarding), cita as fontes
e diz "não sei" quando os documentos não cobrem a pergunta.

Usa `Microsoft.Extensions.AI` (`IChatClient`, `IEmbeddingGenerator`) e
`Microsoft.Extensions.VectorData`. O Semantic Kernel entra como
adaptador (`Rag.SemanticKernel`).

**Roda 100% local, sem conta e sem custo** (Ollama), e o mesmo código
roda no Azure AI Foundry trocando só a configuração.

> O GitHub Models, provedor em que o artigo foi planejado, foi
> aposentado pelo GitHub em 30/07/2026. Aqui o provedor é só
> configuração.

## Requisitos

- [SDK do .NET 10](https://dotnet.microsoft.com/download)
- Docker, só para o modelo real local (cerca de 2,2 GB de modelos)

## 1. Build e testes (offline, sem rede, sem token)

```bash
dotnet build
dotnet test
```

Esperado: 34 aprovados. Os testes usam fakes determinísticos atrás das
mesmas interfaces; validam a **mecânica** (chunking, reindexação,
limiar, filtro de acesso, proteção contra injeção, citações,
retentativas), não a qualidade semântica de um modelo.

## 2. Opção A: Ollama (local, executada pelo autor)

```bash
docker run -d -v ollama:/root/.ollama -p 11434:11434 \
  --name ollama ollama/ollama
docker exec ollama ollama pull nomic-embed-text   # embeddings, 768 dim
docker exec ollama ollama pull qwen2.5:3b         # chat, 3B, roda em CPU
```

```bash
cd src/Rag.Api
Profile=Ollama ASPNETCORE_ENVIRONMENT=Development \
  dotnet run -- --ask "Quem aprova um hotfix fora da janela de deploy?"
Profile=Ollama ASPNETCORE_ENVIRONMENT=Development \
  dotnet run -- --eval
```

Medido pelo autor (só CPU, sem GPU, 16 perguntas-ouro): hit rate 100%,
MRR 1,00, abstenção correta 3/3, respondidas 10 de 13 perguntas
cobertas (77%), média de 25,2 s por pergunta. É um modelo pequeno e
local: a qualidade é menor que a de um modelo grande hospedado. O
`appsettings.Ollama.json` tem o endpoint (`http://localhost:11434/v1`),
os modelos, o tamanho do vetor (768) e o `MinScore`.

Parar e limpar:

```bash
docker stop ollama && docker rm ollama
docker volume rm ollama        # apaga os modelos baixados
```

## 3. Opção B: Azure AI Foundry (da documentação, NÃO executada pelo autor)

Crie um recurso e um projeto Foundry, publique um modelo de chat e um
de embeddings e mantenha endpoint e chave fora do código:

```bash
cd src/Rag.Api
dotnet user-secrets set "Ai:ApiKey" "<sua-chave>"
dotnet user-secrets set "Ai:Endpoint" \
  "https://<seu-recurso>.services.ai.azure.com/openai/v1/"
```

O perfil já usa `gpt-5-mini` + `text-embedding-3-small` (1536 dim); o
`gpt-5-mini` é modelo de raciocínio, então `temperature` não é enviada e
valem `max_completion_tokens` / `reasoning_effort`. Coloque os nomes dos
seus deployments no `appsettings.Foundry.json` (sem segredos) e rode com `Profile=Foundry`. Sem chave, use o Microsoft
Entra ID: `Ai__Auth=EntraId` (`DefaultAzureCredential`; muda só a
criação do cliente em `OpenAiClientFactory`). O passo a passo, com os
links da documentação oficial, está no artigo. Consulte a página oficial
de preços: esta opção é paga por uso.

## 4. Use os seus dados

Coloque arquivos `.md` ou `.txt` em `docs-meus/` (ignorada pelo git) e
rode:

```bash
Documents__Path=docs-meus dotnet run --project src/Rag.Api
```

Os metadados por documento ficam num frontmatter (`title`, `version`,
`area`, `access`: public | team | restricted). Para as suas perguntas-
ouro, crie `eval/meu-golden.json` (ignorado pelo git) e use
`Eval__GoldenSet=eval/meu-golden.json` com `-- --eval`. PDF, Word e
HTML não são lidos: escreva outra `IDocumentSource`. Não indexe
segredos nem dados pessoais crus.

## API (modo offline por padrão)

```bash
cd src/Rag.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run
curl -X POST http://localhost:5025/api/v1/ask \
  -H "Content-Type: application/json" \
  -H "X-Dev-User: ana" -H "X-Dev-Level: Team" \
  -d '{"question":"Posso fazer deploy na sexta-feira?"}'
```

A autenticação `DevHeader` é só para desenvolvimento (o nível de acesso
vem do token, nunca do corpo). `POST /api/v1/admin/reindex` exige o
papel `rag-admin` (`X-Dev-Admin: true`).

## Trocar o vector store

`VectorStore:Provider` = `InMemory` (padrão) ou `Postgres` (pgvector,
com `VectorStore:ConnectionString`). Outros provedores seguem o mesmo
molde: um pacote `CommunityToolkit.VectorData.*` e um `case` na
`VectorStoreFactory`. Só o InMemory foi executado.

## Estrutura

```
src/
  Rag.Domain/          Chunk, SourceDocument, Citation, AccessLevel
  Rag.Application/     ingestão, recuperação, prompt, guardrails,
                       avaliação (portas: IChunkStore, IDocumentSource)
  Rag.Infrastructure/  store VectorData, cliente compatível com OpenAI,
                       resiliência, fakes offline, DI
  Rag.SemanticKernel/  busca como plugin do Semantic Kernel / AIFunction
  Rag.Api/             Minimal API, comandos --ask e --eval
tests/Rag.Tests/       xUnit, sem rede
docs-exemplo/          documentos de exemplo fictícios
docs-meus/             os seus documentos (ignorada pelo git)
eval/golden.json       conjunto de perguntas-ouro
```
