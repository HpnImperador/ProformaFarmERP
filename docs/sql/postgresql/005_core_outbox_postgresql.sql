-- Core.Outbox - Script idempotente (PostgreSQL)
-- Objetivo: infraestrutura de Domain Events + Outbox Pattern

CREATE SCHEMA IF NOT EXISTS "Core";

CREATE TABLE IF NOT EXISTS "Core"."OutboxEvent" (
    "Id" uuid PRIMARY KEY,
    "OrganizacaoId" integer NOT NULL,
    "EventType" varchar(300) NOT NULL,
    "Payload" text NOT NULL,
    "OccurredOnUtc" timestamp with time zone NOT NULL,
    "CorrelationId" uuid,
    "Status" smallint NOT NULL DEFAULT 0,
    "RetryCount" integer NOT NULL DEFAULT 0,
    "NextAttemptUtc" timestamp with time zone NOT NULL DEFAULT now(),
    "ProcessedOnUtc" timestamp with time zone,
    "LockedUntilUtc" timestamp with time zone,
    "LastError" varchar(2000)
);

CREATE INDEX IF NOT EXISTS "IX_Core_OutboxEvent_Status_NextAttemptUtc"
    ON "Core"."OutboxEvent" ("Status", "NextAttemptUtc", "OccurredOnUtc");

CREATE INDEX IF NOT EXISTS "IX_Core_OutboxEvent_Organizacao_Status"
    ON "Core"."OutboxEvent" ("OrganizacaoId", "Status", "OccurredOnUtc");

CREATE TABLE IF NOT EXISTS "Core"."OutboxProcessedEvent" (
    "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "EventId" uuid NOT NULL,
    "HandlerName" varchar(200) NOT NULL,
    "ProcessedOnUtc" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Core_OutboxProcessedEvent_Event_Handler"
    ON "Core"."OutboxProcessedEvent" ("EventId", "HandlerName");

CREATE TABLE IF NOT EXISTS "Core"."OutboxHelloProbe" (
    "IdOutboxHelloProbe" uuid PRIMARY KEY,
    "OrganizacaoId" integer NOT NULL,
    "NomeEvento" varchar(120) NOT NULL,
    "SimularFalhaUmaVez" boolean NOT NULL DEFAULT false,
    "ProcessedCount" integer NOT NULL DEFAULT 0,
    "CriadoEmUtc" timestamp with time zone NOT NULL DEFAULT now(),
    "UltimoProcessamentoUtc" timestamp with time zone
);

CREATE TABLE IF NOT EXISTS "Core"."EstoqueBaixoNotificacao" (
    "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "EventId" uuid NOT NULL,
    "OrganizacaoId" integer NOT NULL,
    "IdUnidadeOrganizacional" integer NOT NULL,
    "IdProduto" integer NOT NULL,
    "IdLote" integer,
    "QuantidadeDisponivel" numeric(18,4) NOT NULL,
    "QuantidadeReservada" numeric(18,4) NOT NULL,
    "QuantidadeLiquida" numeric(18,4) NOT NULL,
    "LimiteEstoqueBaixo" numeric(18,4) NOT NULL,
    "OrigemMovimento" varchar(40) NOT NULL,
    "DocumentoReferencia" varchar(120),
    "CorrelationId" uuid,
    "DetectadoEmUtc" timestamp with time zone NOT NULL,
    "RegistradoEmUtc" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Core_EstoqueBaixoNotificacao_EventId"
    ON "Core"."EstoqueBaixoNotificacao" ("EventId");

CREATE INDEX IF NOT EXISTS "IX_Core_EstoqueBaixoNotificacao_Org_Produto"
    ON "Core"."EstoqueBaixoNotificacao" ("OrganizacaoId", "IdProduto", "DetectadoEmUtc" DESC);

CREATE TABLE IF NOT EXISTS "Core"."EstoqueRepostoNotificacao" (
    "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "EventId" uuid NOT NULL,
    "OrganizacaoId" integer NOT NULL,
    "IdUnidadeOrganizacional" integer NOT NULL,
    "IdProduto" integer NOT NULL,
    "IdLote" integer,
    "QuantidadeLiquidaAntes" numeric(18,4) NOT NULL,
    "QuantidadeLiquidaDepois" numeric(18,4) NOT NULL,
    "LimiteEstoqueBaixo" numeric(18,4) NOT NULL,
    "OrigemMovimento" varchar(40) NOT NULL,
    "DocumentoReferencia" varchar(120),
    "CorrelationId" uuid,
    "DetectadoEmUtc" timestamp with time zone NOT NULL,
    "RegistradoEmUtc" timestamp with time zone NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Core_EstoqueRepostoNotificacao_EventId"
    ON "Core"."EstoqueRepostoNotificacao" ("EventId");

CREATE INDEX IF NOT EXISTS "IX_Core_EstoqueRepostoNotificacao_Org_Produto"
    ON "Core"."EstoqueRepostoNotificacao" ("OrganizacaoId", "IdProduto", "DetectadoEmUtc" DESC);
