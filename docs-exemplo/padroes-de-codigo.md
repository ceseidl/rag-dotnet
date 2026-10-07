---
title: Padrões de Código do Time Pedidos
version: 2.0
access: team
---

# Padrões de Código

## Arquitetura

Os serviços seguem Clean Architecture em quatro camadas: Domain, Application, Infrastructure e Api. A dependência aponta sempre para dentro: o Domain não referencia ninguém e a Api só conhece Application e Infrastructure para registrar a injeção de dependência.

## Testes

Usamos xUnit. A camada Application precisa de cobertura mínima de 80% de linhas. Testes de integração sobem a API em memória com WebApplicationFactory e nunca chamam serviços externos reais.

Teste que depende de relógio, rede ou ordem de execução deve ser corrigido ou removido. Teste intermitente não pode ficar na pipeline.

## Convenções

Nomes de métodos assíncronos terminam em Async e recebem um CancellationToken. Datas são sempre UTC. Não usamos `DateTime.Now`. Configurações vêm de IOptions e segredos nunca entram no repositório.

## Commits e branches

As mensagens de commit seguem o formato `tipo: descrição`, com os tipos feat, fix, docs, test e chore. Branches de trabalho usam o prefixo `feature/` ou `fix/` seguido do número da tarefa.
