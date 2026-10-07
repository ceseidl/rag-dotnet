---
title: ADR-0007 Mensageria entre serviços
version: 1.0
access: public
---

# ADR-0007: Mensageria entre serviços

Status: aceita.

## Contexto

O serviço de pedidos precisa avisar estoque, pagamentos e notificações quando um pedido muda de estado. Chamadas HTTP síncronas entre todos os serviços deixaram o checkout lento e frágil: uma falha em notificações derrubava a criação do pedido.

## Decisão

Adotamos o RabbitMQ como broker, usado por meio do MassTransit. Cada serviço publica eventos de domínio e assina só o que precisa. Para não perder mensagens, usamos o padrão outbox: o evento é gravado na mesma transação do pedido e um processo separado o publica no broker.

## Alternativas consideradas

O Kafka foi avaliado e rejeitado por ora: nosso volume é de poucos milhares de mensagens por minuto, e a operação de um cluster Kafka custaria mais do que o ganho. Filas gerenciadas da nuvem foram descartadas para manter o ambiente local idêntico ao de produção com um único `docker compose up`.

## Consequências

Os consumidores precisam ser idempotentes, porque o outbox garante entrega pelo menos uma vez. Mensagens que falham três vezes vão para a fila de mensagens mortas, monitorada por alerta. Revisitamos esta decisão se o volume passar de 100 mil mensagens por minuto.
