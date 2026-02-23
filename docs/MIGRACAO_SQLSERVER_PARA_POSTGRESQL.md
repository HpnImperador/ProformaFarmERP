# Migração SQL Server para PostgreSQL (Plano Oficial)

## 1. Objetivo
Definir e executar a migração do banco de dados do ProformaFarm ERP de SQL Server para PostgreSQL, mantendo continuidade operacional, segurança por `OrgContext` e rastreabilidade do pipeline `Outbox + Event Relay`.

## 2. Ambiente-alvo validado
- Servidor de laboratório: Ubuntu Server.
- Serviços já disponíveis:
  - PostgreSQL
  - N8N
  - Prisma

Esse ambiente será usado como referência de homologação da trilha PostgreSQL.

## 3. Estratégia de migração
### 3.1 Abordagem
- Migração incremental por trilha paralela (branch dedicada).
- SQL Server permanece como base estável até o corte final.
- PostgreSQL entra primeiro como alvo de homologação técnica.

### 3.2 Fases
1. Foundation de provider:
   - Configuração de `Database:Provider` no backend.
   - Suporte a `DefaultConnection` (SQL Server) e `PostgresConnection` (PostgreSQL).
2. Compatibilização de persistência:
   - EF Core com `UseNpgsql`.
   - Dapper via `NpgsqlConnection`.
3. Conversão de SQL:
   - Scripts idempotentes em versão PostgreSQL.
   - Ajustes de sintaxe T-SQL para PostgreSQL.
4. Validação:
   - `dotnet build`
   - Testes de integração no banco PostgreSQL.
5. Cutover:
   - janela controlada
   - backup/restauração validados
   - rollback documentado.

## 4. Mudanças já concluídas nesta fase
- Chave de configuração de provider adicionada:
  - `Database:Provider` (`SqlServer` ou `PostgreSql`).
- `Program.cs` preparado para alternar `UseSqlServer` / `UseNpgsql`.
- Fábrica de conexões Dapper preparada para SQL Server e PostgreSQL no mesmo contrato (`ISqlConnectionFactory`).
- `appsettings*.json` com `PostgresConnection`.
- `appsettings.Lab.json` para homologação no Ubuntu Server.

## 4.1 Scripts PostgreSQL disponibilizados
Foram criadas versões idempotentes dos scripts core em:

- `docs/sql/postgresql/001_estrutura_organizacional_postgresql.sql`
- `docs/sql/postgresql/002_seed_estrutura_organizacional_postgresql.sql`
- `docs/sql/postgresql/003_idx_lotacaousuario_orgcontext_postgresql.sql`
- `docs/sql/postgresql/004_estoque_basico_postgresql.sql`
- `docs/sql/postgresql/005_core_outbox_postgresql.sql`
- `docs/sql/postgresql/006_integration_event_relay_postgresql.sql`

Ordem de execução recomendada no PostgreSQL:
1. `001`
2. `002`
3. `003`
4. `004`
5. `005`
6. `006`

## 5. Checklist técnico de compatibilidade
- Tipos:
  - `uniqueidentifier` -> `uuid`
  - `datetimeoffset` -> `timestamp with time zone`
  - `bit` -> `boolean`
- SQL:
  - `TOP` -> `LIMIT`
  - `GETUTCDATE`/`SYSUTCDATETIME` -> `now() at time zone 'utc'` (ou `CURRENT_TIMESTAMP`)
  - `OUTPUT INSERTED` -> `RETURNING`
  - `MERGE` -> `INSERT ... ON CONFLICT ... DO UPDATE`
  - hints SQL Server (`WITH (UPDLOCK, READPAST, ROWLOCK)`) -> estratégia equivalente em PostgreSQL (`FOR UPDATE SKIP LOCKED`)
- Índices/constraints:
  - revisar índices filtrados e constraints específicas.

## 6. Integração com N8N e Prisma
- N8N:
  - consumir webhooks do `Event Relay` para automações externas.
  - usar `CorrelationId` para trilha operacional.
- Prisma:
  - opcional para integrações de leitura/serviços auxiliares.
  - não substituir EF Core/Dapper no core do monólito sem decisão arquitetural formal.

## 7. Comandos de referência
```powershell
dotnet restore
dotnet build
dotnet test ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj
```

### 7.2 Execução segura via dev-loop (safe mode)
```powershell
powershell -ExecutionPolicy Bypass -File scripts/dev-loop.ps1 `
  -ApplyPostgresScripts `
  -PostgresConnection "host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>" `
  -AllowedPostgresDatabases proformafarm `
  -AllowedPostgresHosts <ubuntu_server_ip> `
  -AcknowledgeSharedPostgres
```

Referência operacional:
- `docs/CHECKLIST_PRE_EXECUCAO_POSTGRESQL.md`

### 7.1 Execução de scripts no Ubuntu (exemplo com psql)
```bash
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/001_estrutura_organizacional_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/002_seed_estrutura_organizacional_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/003_idx_lotacaousuario_orgcontext_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/004_estoque_basico_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/005_core_outbox_postgresql.sql
psql \"host=<ubuntu_server_ip> port=5432 dbname=proformafarm user=<postgres_user> password=<postgres_password>\" -f docs/sql/postgresql/006_integration_event_relay_postgresql.sql
```

## 8. Critérios de pronto para corte
- Build e suíte de integração verdes com PostgreSQL.
- Scripts centrais (`Organização`, `Estoque`, `Outbox`, `Relay`) executados com sucesso no Ubuntu.
- Monitoramento operacional e logs com `CorrelationId` validados.
- Plano de rollback testado.
