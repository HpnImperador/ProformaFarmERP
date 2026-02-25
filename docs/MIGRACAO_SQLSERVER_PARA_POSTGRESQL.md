# MigraÃ§Ã£o SQL Server para PostgreSQL (Plano Oficial)

## 1. Objetivo
Definir e executar a migraÃ§Ã£o do banco de dados do ProformaFarm ERP de SQL Server para PostgreSQL, mantendo continuidade operacional, seguranÃ§a por `OrgContext` e rastreabilidade do pipeline `Outbox + Event Relay`.

## 2. Ambiente-alvo validado
- Servidor de laboratÃ³rio: Ubuntu Server.
- ServiÃ§os jÃ¡ disponÃ­veis:
  - PostgreSQL
  - N8N
  - Prisma

Esse ambiente serÃ¡ usado como referÃªncia de homologaÃ§Ã£o da trilha PostgreSQL.

## 3. EstratÃ©gia de migraÃ§Ã£o
### 3.1 Abordagem
- MigraÃ§Ã£o incremental por trilha paralela (branch dedicada).
- SQL Server permanece como base estÃ¡vel atÃ© o corte final.
- PostgreSQL entra primeiro como alvo de homologaÃ§Ã£o tÃ©cnica.

### 3.2 Fases
1. Foundation de provider:
   - ConfiguraÃ§Ã£o de `Database:Provider` no backend.
   - Suporte a `DefaultConnection` (SQL Server) e `PostgresConnection` (PostgreSQL).
2. CompatibilizaÃ§Ã£o de persistÃªncia:
   - EF Core com `UseNpgsql`.
   - Dapper via `NpgsqlConnection`.
3. ConversÃ£o de SQL:
   - Scripts idempotentes em versÃ£o PostgreSQL.
   - Ajustes de sintaxe T-SQL para PostgreSQL.
4. ValidaÃ§Ã£o:
   - `dotnet build`
   - Testes de integraÃ§Ã£o no banco PostgreSQL.
5. Cutover:
   - janela controlada
   - backup/restauraÃ§Ã£o validados
   - rollback documentado.

## 4. MudanÃ§as jÃ¡ concluÃ­das nesta fase
- Chave de configuraÃ§Ã£o de provider adicionada:
  - `Database:Provider` (`SqlServer` ou `PostgreSql`).
- `Program.cs` preparado para alternar `UseSqlServer` / `UseNpgsql`.
- FÃ¡brica de conexÃµes Dapper preparada para SQL Server e PostgreSQL no mesmo contrato (`ISqlConnectionFactory`).
- `appsettings*.json` com `PostgresConnection`.
- `appsettings.Lab.json` para homologaÃ§Ã£o no Ubuntu Server.

## 4.1 Scripts PostgreSQL disponibilizados
Foram criadas versÃµes idempotentes dos scripts core em:

- `docs/sql/postgresql/000_auth_base_postgresql.sql`
- `docs/sql/postgresql/001_estrutura_organizacional_postgresql.sql`
- `docs/sql/postgresql/002_seed_estrutura_organizacional_postgresql.sql`
- `docs/sql/postgresql/003_idx_lotacaousuario_orgcontext_postgresql.sql`
- `docs/sql/postgresql/004_estoque_basico_postgresql.sql`
- `docs/sql/postgresql/005_core_outbox_postgresql.sql`
- `docs/sql/postgresql/006_integration_event_relay_postgresql.sql`
- `docs/sql/postgresql/007_compat_unquoted_aliases_postgresql.sql`

Ordem de execuÃ§Ã£o recomendada no PostgreSQL:
1. `000`
1. `001`
2. `002`
3. `003`
4. `004`
5. `005`
6. `006`
7. `007`

## 5. Checklist tÃ©cnico de compatibilidade
- Tipos:
  - `uniqueidentifier` -> `uuid`
  - `datetimeoffset` -> `timestamp with time zone`
  - `bit` -> `boolean`
- SQL:
  - `TOP` -> `LIMIT`
  - `GETUTCDATE`/`SYSUTCDATETIME` -> `now() at time zone 'utc'` (ou `CURRENT_TIMESTAMP`)
  - `OUTPUT INSERTED` -> `RETURNING`
  - `MERGE` -> `INSERT ... ON CONFLICT ... DO UPDATE`
  - hints SQL Server (`WITH (UPDLOCK, READPAST, ROWLOCK)`) -> estratÃ©gia equivalente em PostgreSQL (`FOR UPDATE SKIP LOCKED`)
- Ãndices/constraints:
  - revisar Ã­ndices filtrados e constraints especÃ­ficas.

Matriz de gaps atual:
- `docs/MATRIZ_GAPS_POSTGRESQL.md`

## 6. IntegraÃ§Ã£o com N8N e Prisma
- N8N:
  - consumir webhooks do `Event Relay` para automaÃ§Ãµes externas.
  - usar `CorrelationId` para trilha operacional.
- Prisma:
  - opcional para integraÃ§Ãµes de leitura/serviÃ§os auxiliares.
  - nÃ£o substituir EF Core/Dapper no core do monÃ³lito sem decisÃ£o arquitetural formal.

## 7. Comandos de referÃªncia
```powershell
dotnet restore
dotnet build
dotnet test ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj
```

### 7.2 ExecuÃ§Ã£o segura via dev-loop (safe mode)
```powershell
powershell -ExecutionPolicy Bypass -File scripts/dev-loop.ps1 `
  -ApplyPostgresScripts `
  -PostgresConnection "host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>" `
  -AllowedPostgresDatabases proformafarm `
  -AllowedPostgresHosts <ubuntu_server_ip> `
  -AcknowledgeSharedPostgres
```

ReferÃªncia operacional:
- `docs/CHECKLIST_PRE_EXECUCAO_POSTGRESQL.md`

### 7.1 ExecuÃ§Ã£o de scripts no Ubuntu (exemplo com psql)
```bash
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/001_estrutura_organizacional_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/002_seed_estrutura_organizacional_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/003_idx_lotacaousuario_orgcontext_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/004_estoque_basico_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/005_core_outbox_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/006_integration_event_relay_postgresql.sql
```

## 8. CritÃ©rios de pronto para corte
- Build e suÃ­te de integraÃ§Ã£o verdes com PostgreSQL.
- Scripts centrais (`OrganizaÃ§Ã£o`, `Estoque`, `Outbox`, `Relay`) executados com sucesso no Ubuntu.
- Monitoramento operacional e logs com `CorrelationId` validados.
- Plano de rollback testado.

## 9. Validação prática do Outbox + Event Relay no laboratório
Para validar o pipeline ponta a ponta no PostgreSQL (sem impacto em outros bancos), use o modo dedicado no `dev-loop`:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/dev-loop.ps1 `
  -ValidatePostgresOutboxRelay `
  -PostgresConnection "host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>" `
  -AllowedPostgresDatabases proformafarm `
  -AllowedPostgresHosts <ubuntu_server_ip> `
  -AcknowledgeSharedPostgres `
  -TestProject "ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj"
```

O que o modo faz automaticamente:
- valida `safe mode` (database e host allowlist);
- captura snapshot pré e pós execução do estado de `Core.OutboxEvent` e `Integration.IntegrationDeliveryLog`;
- executa os testes de integração filtrados de Outbox com provider PostgreSQL.

Evidências mínimas para anexar no checklist:
1. Console com snapshots pré/pós.
2. Resultado `dotnet test` com suíte de Outbox verde.
3. Confirmação de ausência de impacto em outros bancos do servidor compartilhado.

### 9.1 Runner pronto para laboratório (com log de evidência)
Também foi disponibilizado um script wrapper para execução com log automático:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/lab-validate-postgres-outbox-relay.ps1 `
  -PostgresHost <ubuntu_server_ip> `
  -PostgresDatabase proformafarm `
  -PostgresUser <postgres_user> `
  -PostgresPassword <postgres_password>
```

Saída esperada:
- execução do `dev-loop` em modo de validação PostgreSQL;
- arquivo de log em `logs/lab-postgres-outbox-relay-<timestamp>.log` para anexar no checklist.

### 9.2 Validação completa em lote (script único)
Para executar uma bateria completa de validação de migração (precheck, scripts idempotentes, suíte completa de testes em PostgreSQL e validação dedicada do Outbox/Relay), use:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/migration-validation.ps1 `
  -PostgresHost <ubuntu_server_ip> `
  -PostgresPort 5432 `
  -PostgresDatabase proformafarm `
  -PostgresUser <postgres_user> `
  -PostgresPassword <postgres_password> `
  -PsqlPath "<caminho_para_psql.exe>"
```

Saída esperada:
- log técnico detalhado em `logs/migration-validation-<timestamp>.log`;
- relatório executivo em `logs/migration-validation-<timestamp>.md`;
- validação final aprovada para continuidade da migração.

Observação:
- por padrão o script roda uma suíte filtrada de *readiness PostgreSQL* (autenticação) para evitar falso negativo de cenários ainda acoplados a SQL Server.
- para incluir também a validação dedicada de Outbox/Relay no mesmo fluxo, adicione `-IncludeOutboxValidation`.
