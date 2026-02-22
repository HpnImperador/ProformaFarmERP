# ROADMAP ATUALIZADO – INTEGRACAO N8N

## Fase 0 – Fundacional

- Concluir Outbox + Domain Events.
- Criar domínio Integracao.
- Implementar IntegrationClient.
- Implementar IntegrationDeliveryLog.
- Implementar IdempotencyLog.
- Criar EventRelay (BackgroundService).

## Fase 1 – Eventos Controlados

- Publicar VendaFinalizada.
- Publicar EstoqueBaixo.
- Implementar assinatura HMAC.
- Testes de retry e idempotência.

## Fase 2 – Inbound Seguro

- Criar Integration API protegida por API Key.
- Derivar OrganizacaoId da credencial.
- Implementar Idempotency-Key obrigatório.
- Testes de concorrência.

## Fase 3 – Observabilidade

- Métricas de eventos entregues.
- Alertas de falha.
- Dashboard interno de integrações.

## Regra de Evolução

Nenhuma automação fiscal ou financeira crítica deve ser delegada ao n8n sem:

- Auditoria validada
- Idempotência comprovada
- Testes de carga executados
