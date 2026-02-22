# Checklist de Merge - Outbox + Domain Events

## Critérios obrigatórios (gate de merge)

- [ ] `OrganizacaoId` não vem do client e é derivado do `OrgContext`.
- [ ] Persistência no Outbox ocorre na mesma transação do comando (`SaveChanges`).
- [ ] Handler é idempotente por `EventId` (com deduplicação por handler).
- [ ] Worker evita corrida com estratégia de claim/lock (`UPDLOCK` + `READPAST`).
- [ ] Retry/backoff está habilitado e falhas não travam o pipeline.
- [ ] Logs possuem `EventId` e `CorrelationId`.
- [ ] Não há risco de vazamento cross-tenant em handlers (escopo por organização).
- [ ] Testes de integração cobrem persistência, processamento, retry e idempotência.

## Evidências mínimas esperadas na PR

- script idempotente: `docs/sql/005_core_outbox.sql`;
- notas técnicas: `docs/architecture/outbox-implementation-notes.md`;
- testes de integração: `ProformaFarm.Application.Tests/Integration/Outbox/OutboxPipelineEndpointTests.cs`;
- comandos executados e resultado:
  - `dotnet build ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj`
  - `dotnet test ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj --filter "FullyQualifiedName~Integration.Outbox" --no-build`

## Resultado da rodada atual

- Status: **Aprovado para evolução**
- Observação: manter o `Hello Event` como prova de vida obrigatória antes de introduzir novos eventos de domínio críticos.
