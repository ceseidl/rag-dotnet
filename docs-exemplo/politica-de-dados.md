---
title: Política de Dados Pessoais nos Logs
version: 1.2
access: team
---

# Dados Pessoais nos Logs

## O que nunca vai para o log

CPF, e-mail, telefone, endereço e dados de cartão nunca podem aparecer em logs, traces ou mensagens de exceção. Registre o identificador interno do cliente no lugar.

Quando for inevitável registrar um documento para diagnóstico, mascare: mantenha só os dois últimos dígitos, por exemplo `*********01`.

## Retenção

Logs de aplicação ficam 30 dias no armazenamento quente e são apagados depois. Logs de auditoria, que registram quem acessou o quê, ficam 12 meses. Dados de clientes em ambientes de teste devem ser anonimizados antes da carga.

## Incidente de vazamento

Se descobrir dado pessoal em log, apague o registro, avise o encarregado de dados em até 24 horas e abra um incidente. Não copie o dado vazado para o chamado.
