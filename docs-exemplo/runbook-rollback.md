---
title: Runbook de Rollback
version: 1.4
access: team
---

# Runbook de Rollback

Use este runbook quando uma versão recém-publicada causar erros ou lentidão em produção.

## Quando reverter

Reverta primeiro e investigue depois. Se a taxa de erro 5xx passou de 1% ou a latência p95 dobrou depois de um deploy, não tente corrigir em produção: execute o rollback.

## Passo a passo

1. Abra o pipeline de rollback do serviço e informe a tag da última versão estável, por exemplo `v2.3.0`.
2. Aguarde o pipeline reimplantar a imagem anterior. Leva em média 4 minutos.
3. Confirme o endpoint `/health` do serviço: ele precisa responder 200.
4. Observe o painel de erros por 10 minutos e confirme que a taxa voltou ao normal.
5. Avise no canal #orders-ops que o rollback foi feito, com a versão revertida e o motivo.

## Migrations de banco

Nunca reverta uma migration que apagou ou alterou dados. Migrations devem ser compatíveis com a versão anterior do código, para que o rollback do código não exija mexer no banco. Se uma migration destrutiva já foi aplicada, corrija com uma nova migration para frente (forward-fix) e acione o tech lead.

## Depois do rollback

Abra um registro de incidente com a linha do tempo e marque o pull request que causou o problema. A feature flag da funcionalidade deve ser desligada antes de uma nova tentativa de deploy.
