# docs-meus

Coloque aqui os SEUS documentos (`.md` ou `.txt`). Esta pasta é
ignorada pelo git: nada do que você colocar aqui entra no repositório.

Metadados opcionais no topo de cada arquivo:

```
---
title: Runbook de rollback do serviço X
version: 1.4
area: plataforma
access: team
---
```

`access`: `public`, `team` ou `restricted`. Sem frontmatter, o título é
o nome do arquivo, a versão é `1` e o acesso é `public`.

Aponte a ingestão para esta pasta:

```bash
Documents__Path=docs-meus dotnet run --project src/Rag.Api
```

Para avaliar com as suas perguntas, crie `eval/meu-golden.json` (mesmo
formato de `eval/golden.json`) e rode:

```bash
Documents__Path=docs-meus Eval__GoldenSet=eval/meu-golden.json \
  dotnet run --project src/Rag.Api -- --eval
```

Não indexe segredos (senhas, tokens, chaves), dados pessoais sem
mascaramento nem documentos que o provedor de IA não deva receber.
Este README e arquivos que começam com `_` ou `.` não são indexados.
