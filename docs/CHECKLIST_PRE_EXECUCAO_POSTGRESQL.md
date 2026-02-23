# Checklist de Pré-Execução PostgreSQL (Servidor Compartilhado)

## Objetivo
Evitar impacto em outros sistemas conectados ao mesmo servidor PostgreSQL.

## 1. Validação de alvo
1. Confirmar o host autorizado (laboratório).
2. Confirmar o banco dedicado do ProformaFarm (`proformafarm`).
3. Confirmar usuário técnico dedicado (sem privilégios globais).

## 2. Segurança operacional
1. Executar sempre scripts idempotentes em `docs/sql/postgresql/`.
2. Não executar comandos destrutivos fora de janela controlada.
3. Garantir backup recente antes da execução.

## 3. Execução segura (dev-loop)
Usar `safe mode` com confirmação explícita:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/dev-loop.ps1 `
  -ApplyPostgresScripts `
  -PostgresConnection "host=<ip> port=5432 dbname=proformafarm user=<user> password=<pwd>" `
  -AllowedPostgresDatabases proformafarm `
  -AllowedPostgresHosts <ip_do_laboratorio> `
  -AcknowledgeSharedPostgres `
  -SkipRestore -SkipBuild -SkipTest
```

## 4. Pós-execução
1. Validar tabelas core (`Organizacao`, `Estoque`, `Core.OutboxEvent`, `Integration.IntegrationDeliveryLog`).
2. Executar testes de integração filtrados.
3. Registrar log de execução (data/hora, host, database, scripts aplicados).
