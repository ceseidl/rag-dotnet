---
title: Runbook de Incidente Severidade 1
version: 3.0
access: restricted
---

# Incidente Severidade 1

Severidade 1 significa indisponibilidade total do checkout ou perda de dados de clientes. Este documento é restrito ao plantão e à liderança.

## Acionamento

O plantonista deve reconhecer o alerta em até 5 minutos. Sem reconhecimento, o alerta escala para o segundo plantonista e, depois de 10 minutos, para o gerente de engenharia.

## Ponte de incidente

Abra a ponte de incidente no canal #orders-sev1 e nomeie três papéis: o comandante do incidente, que decide; o responsável técnico, que investiga; e o responsável pela comunicação, que atualiza a diretoria a cada 30 minutos.

## Acesso de emergência

O acesso de emergência (break-glass) ao banco de produção fica no cofre de segredos da empresa. Para usá-lo, o comandante do incidente precisa de aprovação do gerente de engenharia. Todo uso é auditado e deve ser justificado no registro do incidente em até 24 horas.

## Encerramento

O incidente só é encerrado quando as métricas ficam normais por 30 minutos. Em até 5 dias úteis o time publica o post-mortem sem culpados, com a causa raiz e as ações corretivas.
