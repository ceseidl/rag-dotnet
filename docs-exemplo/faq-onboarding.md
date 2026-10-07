---
title: FAQ de Onboarding do Time Pedidos
version: 1.8
access: public
---

# FAQ de Onboarding

## Como peço acesso aos repositórios?

Abra um chamado no portal de TI escolhendo o perfil "Desenvolvedor Pedidos". O acesso ao Azure DevOps e à VPN sai em até 1 dia útil. Seu buddy acompanha o processo e confirma quando estiver tudo liberado.

## Como rodo o sistema na minha máquina?

Instale o SDK do .NET 10 e o Docker. Na raiz do repositório rode `docker compose up -d` para subir o banco e o RabbitMQ, e depois `dotnet run --project src/Pedidos.Api`. A API fica em `http://localhost:5100`.

## Como funciona o primeiro pull request?

Escolha uma tarefa marcada como "good first issue", abra uma branch a partir da main com o nome `feature/<numero>` e envie o pull request. Todo pull request precisa de um revisor. Seu buddy revisa o primeiro.

## Quais são as cerimônias do time?

A daily acontece todos os dias às 9h30, com 15 minutos. A retrospectiva é quinzenal, às quartas-feiras, e o planejamento acontece na primeira segunda-feira de cada sprint. Sprints duram duas semanas.

## Onde peço ajuda?

Dúvidas técnicas vão para o canal #orders-dev. Problemas de acesso vão para o canal #ti-suporte. Se não souber a quem perguntar, fale com o seu buddy.

## Quanto tempo leva para eu fazer o primeiro deploy?

Em geral, na terceira semana. Antes disso você acompanha dois deploys feitos por outra pessoa do time.
