# Outbox Pattern - Notas de Implementação

## Objetivo
Implementar uma infraestrutura transversal de **Domain Events + Outbox Pattern** para garantir:

- atomicidade entre comando transacional e registro do evento;
- processamento assíncrono resiliente;
- idempotência por `EventId`;
- rastreabilidade por `OrganizacaoId` e `CorrelationId`.

## Componentes implementados

1. **Contrato de Domain Events**
- `IDomainEvent`
- `BaseDomainEvent`
- `AggregateRoot` com coleção de eventos de domínio

2. **Persistência Outbox**
- tabela `Core.OutboxEvent`
- tabela `Core.OutboxProcessedEvent` (idempotência)
- persistência de eventos no Outbox via `OutboxSaveChangesInterceptor`, dentro do `SaveChanges`

3. **Worker de processamento**
- `OutboxProcessorHostedService` (background)
- `OutboxProcessor` com:
  - claim por lote com lock (`UPDLOCK` + `READPAST`);
  - retry com backoff exponencial;
  - transição de status (`Pending`, `Processing`, `Processed`, `Failed`);
  - logging com `EventId` e `CorrelationId`.

4. **Prova de vida (Hello Event)**
- agregado `OutboxHelloProbe`
- evento `HelloOutboxDomainEvent`
- handler `HelloOutboxDomainEventHandler`

5. **Evento de negócio real (Estoque Baixo)**
- evento `EstoqueBaixoDomainEvent` gerado em transações de saída/ajuste/confirmação de reserva;
- gravação no Outbox na mesma transação Dapper da movimentação;
- handler `EstoqueBaixoDomainEventHandler` com persistência em `Core.EstoqueBaixoNotificacao`;
- idempotência do handler com chave única por `EventId`.

## Regras obrigatórias aplicadas

- `OrganizacaoId` vem do **OrgContext** e é propagado ao evento.
- Evento é persistido no Outbox na **mesma transação** da operação.
- Handler não executa duas vezes para o mesmo `EventId` + `HandlerName`.
- Falhas no processamento não quebram a transação principal do comando.

## Como usar

1. Aplique o script:
- `docs/sql/005_core_outbox.sql`

2. Enfileire um evento de prova de vida:
- `POST /api/outbox/hello-event`

3. Gere evento de negócio:
- execute saída/ajuste/confirmação de reserva que deixe saldo líquido no limite de estoque baixo.

4. Dispare processamento imediato (opcional):
- `POST /api/outbox/processar-agora`

5. Processamento automático:
- executado pelo `OutboxProcessorHostedService` conforme configuração em `Outbox` no `appsettings`.

## Configuração

Seção `Outbox`:

- `BatchSize`
- `PollingIntervalSeconds`
- `LockSeconds`
- `MaxRetries`
- `RetryBaseDelaySeconds`

## Testes de integração

Cobertura implementada:

- persistência no Outbox;
- processamento bem-sucedido;
- retry com backoff;
- idempotência por `EventId`;
- cenário de negócio `EstoqueBaixo` (geração + processamento + persistência de notificação).
