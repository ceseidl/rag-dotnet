---
title: Política de Deploy do Time Pedidos
version: 2.3
access: team
---

# Política de Deploy

Esta política vale para todos os serviços do Time Pedidos que rodam em produção.

## Janelas de deploy

Deploys em produção acontecem de segunda a quinta-feira, entre 10h e 16h (horário de Brasília). Não fazemos deploy às sextas-feiras, nos fins de semana nem em vésperas de feriado.

Hotfix de severidade alta é a única exceção à janela. Ele precisa da aprovação do tech lead do time, registrada no pull request, e de uma pessoa de plantão acompanhando a publicação.

## Aprovação e requisitos

Todo deploy exige pelo menos um revisor aprovando o pull request, além do autor. O pipeline precisa estar verde: build, testes unitários, testes de integração e análise estática. Um pull request com teste falhando não pode ser publicado, nem com aprovação.

Mudanças que alteram contrato de API pública ou o esquema do banco exigem um segundo revisor do time de plataforma.

## Estratégia de publicação

A publicação é gradual. O pipeline envia a nova versão para 10% do tráfego (canário) e observa as métricas por 30 minutos. Se a taxa de erro 5xx passar de 1% ou a latência p95 dobrar, o pipeline reverte sozinho. Passando a observação, a versão sobe para 100%.

Funcionalidades novas entram desligadas atrás de uma feature flag. A flag só é ligada depois do deploy, em um passo separado, para que publicar código e liberar comportamento sejam decisões diferentes.

## Comunicação

Antes de publicar, avise no canal #orders-deploys com o nome do serviço e a versão. Depois de publicar, confirme no mesmo canal que o canário foi concluído.
