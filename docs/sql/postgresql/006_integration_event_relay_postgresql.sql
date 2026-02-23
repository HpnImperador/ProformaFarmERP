-- Integration Event Relay - Script idempotente (PostgreSQL)
-- Objetivo: infraestrutura de entrega Outbox -> Webhook

CREATE SCHEMA IF NOT EXISTS "Integration";

CREATE TABLE IF NOT EXISTS "Integration"."IntegrationClient" (
    "IdIntegrationClient" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "OrganizacaoId" integer NOT NULL,
    "Nome" varchar(120) NOT NULL,
    "WebhookUrl" varchar(500) NOT NULL,
    "SecretKey" varchar(300),
    "Ativo" boolean NOT NULL DEFAULT true,
    "CriadoEmUtc" timestamp with time zone NOT NULL DEFAULT now(),
    "AtualizadoEmUtc" timestamp with time zone
);

CREATE INDEX IF NOT EXISTS "IX_IntegrationClient_Organizacao_Ativo"
    ON "Integration"."IntegrationClient" ("OrganizacaoId", "Ativo");

CREATE TABLE IF NOT EXISTS "Integration"."IntegrationDeliveryLog" (
    "IdIntegrationDeliveryLog" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "OutboxEventId" uuid NOT NULL,
    "IdIntegrationClient" integer NOT NULL,
    "OrganizacaoId" integer NOT NULL,
    "EventType" varchar(300) NOT NULL,
    "CorrelationId" uuid,
    "PayloadHash" varchar(64) NOT NULL,
    "Status" smallint NOT NULL DEFAULT 0,
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "NextAttemptUtc" timestamp with time zone NOT NULL DEFAULT now(),
    "LastAttemptUtc" timestamp with time zone,
    "LockedUntilUtc" timestamp with time zone,
    "LastError" varchar(2000),
    "ResponseStatusCode" integer,
    "ResponseBody" varchar(2000),
    "CriadoEmUtc" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "FK_IntegrationDeliveryLog_IntegrationClient"
        FOREIGN KEY ("IdIntegrationClient") REFERENCES "Integration"."IntegrationClient" ("IdIntegrationClient")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_IntegrationDeliveryLog_Outbox_Client"
    ON "Integration"."IntegrationDeliveryLog" ("OutboxEventId", "IdIntegrationClient");

CREATE INDEX IF NOT EXISTS "IX_IntegrationDeliveryLog_Status_NextAttempt"
    ON "Integration"."IntegrationDeliveryLog" ("Status", "NextAttemptUtc", "CriadoEmUtc");

CREATE INDEX IF NOT EXISTS "IX_IntegrationDeliveryLog_Organizacao"
    ON "Integration"."IntegrationDeliveryLog" ("OrganizacaoId", "CriadoEmUtc" DESC);
