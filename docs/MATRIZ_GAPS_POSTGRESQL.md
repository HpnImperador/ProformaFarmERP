# Matriz de Gaps PostgreSQL (Estado Atual)

## Objetivo
Listar, com prioridade, os pontos ainda dependentes de T-SQL no código para migração segura ao PostgreSQL.

## Situação consolidada
- Base multi-provider já implementada.
- Pipeline Outbox/Event Relay já possui fallback de dialeto por provider.
- Ainda existem consultas e comandos T-SQL em módulos operacionais.

## Gaps por prioridade

## Prioridade 0 (bloqueio direto de execução em PostgreSQL)
1. `ProformaFarm/Controllers/EstoqueController.cs`
- Dependências T-SQL:
  - `TOP (@Limite)` e `TOP (1)`.
  - `SYSUTCDATETIME()`.
  - hints de lock `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` e `WITH (READPAST, ROWLOCK)`.
  - `OUTPUT INSERTED`.
- Impacto:
  - Fluxos centrais de estoque, reserva, expiração e movimentação podem falhar em PostgreSQL.

2. `ProformaFarm/Infrastructure/Context/OrgContext.cs`
- Dependência T-SQL: `SELECT TOP (1)`.
- Impacto:
  - Resolução de contexto organizacional pode falhar em PostgreSQL.

3. `ProformaFarm/Controllers/OrganizacaoController.cs`
- Dependência T-SQL: `SELECT TOP (1) @IdOrganizacao = ...`.
- Impacto:
  - Resolução de organização em leitura/consulta pode falhar.

## Prioridade 1 (autenticação e sessão)
1. `ProformaFarm.Infrastructure/Repositories/Auth/UserRepository.cs`
- Dependência T-SQL: `SELECT TOP (1)`.

2. `ProformaFarm.Infrastructure/Repositories/Auth/RefreshTokenRepository.cs`
- Dependências T-SQL:
  - `TOP (1)`.
  - `SYSUTCDATETIME()`.
- Impacto:
  - Fluxo de refresh token e autenticação pode quebrar.

3. `ProformaFarm/Controllers/SeedController.cs`
- Dependência T-SQL: `OUTPUT INSERTED`.
- Observação:
  - Menor risco em produção, mas relevante para setup/homologação.

## Prioridade 2 (compatibilidade de defaults EF)
1. `ProformaFarm.Infrastructure/Data/ProformaFarmDbContext.cs`
- Dependência SQL Server: `HasDefaultValueSql("SYSUTCDATETIME()")`.
- Impacto:
  - Pode gerar inconsistência em migrations/defaults no PostgreSQL.

## Ajustes já resolvidos nesta trilha
- `OutboxProcessor` com seleção de SQL por provider.
- `EventRelayProcessor` com seleção de SQL por provider.
- `HelloOutboxDomainEventHandler` com SQL por provider.

## Plano objetivo de correção
1. Criar utilitário interno de dialeto para Dapper (`LIMIT 1`, `CURRENT_TIMESTAMP`, `RETURNING`, lock claim em PostgreSQL).
2. Refatorar `OrgContext`, `OrganizacaoController`, `UserRepository`, `RefreshTokenRepository`.
3. Refatorar `EstoqueController` por blocos:
   - consultas de leitura;
   - operações transacionais;
   - reservas/expiração;
   - inserts com retorno de id.
4. Ajustar defaults no `DbContext` com escolha por provider.
5. Rodar validação:
   - build;
   - testes de integração críticos;
   - execução segura no lab com `dev-loop` em safe mode.

## Critério de pronto da etapa de compatibilização
- Nenhum comando SQL Server-specific em caminho crítico de runtime quando `Database:Provider=PostgreSql`.
- Login, OrgContext, Estoque e Outbox/EventRelay executando no PostgreSQL sem fallback manual.
