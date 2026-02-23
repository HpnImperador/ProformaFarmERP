-- Proforma Farm ERP - Estrutura Organizacional (PostgreSQL)
-- Script: 001_estrutura_organizacional_postgresql.sql
-- Objetivo: criacao idempotente das tabelas e indices da estrutura organizacional.

BEGIN;

CREATE TABLE IF NOT EXISTS "Organizacao" (
    "IdOrganizacao" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "RazaoSocial" varchar(200) NOT NULL,
    "NomeFantasia" varchar(200),
    "Cnpj" char(14) NOT NULL,
    "Ativa" boolean NOT NULL DEFAULT true,
    "DataCriacao" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "CK_Organizacao_Cnpj_Formato" CHECK ("Cnpj" ~ '^[0-9]{14}$')
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Organizacao_Cnpj"
    ON "Organizacao" ("Cnpj");

CREATE TABLE IF NOT EXISTS "UnidadeOrganizacional" (
    "IdUnidadeOrganizacional" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "IdUnidadePai" integer,
    "Tipo" varchar(40) NOT NULL,
    "Codigo" varchar(30) NOT NULL,
    "Nome" varchar(200) NOT NULL,
    "Ativa" boolean NOT NULL DEFAULT true,
    "DataInicio" timestamp with time zone NOT NULL,
    "DataFim" timestamp with time zone,
    CONSTRAINT "FK_UnidadeOrganizacional_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao"),
    CONSTRAINT "FK_UnidadeOrganizacional_UnidadePai" FOREIGN KEY ("IdUnidadePai") REFERENCES "UnidadeOrganizacional" ("IdUnidadeOrganizacional"),
    CONSTRAINT "CK_UnidadeOrganizacional_DataVigencia" CHECK ("DataFim" IS NULL OR "DataFim" >= "DataInicio"),
    CONSTRAINT "CK_UnidadeOrganizacional_SemAutoReferencia" CHECK ("IdUnidadePai" IS NULL OR "IdUnidadePai" <> "IdUnidadeOrganizacional")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_UnidadeOrganizacional_Org_Codigo"
    ON "UnidadeOrganizacional" ("IdOrganizacao", "Codigo");

CREATE INDEX IF NOT EXISTS "IX_UnidadeOrganizacional_Org_Pai_Ativa"
    ON "UnidadeOrganizacional" ("IdOrganizacao", "IdUnidadePai", "Ativa");

CREATE INDEX IF NOT EXISTS "IX_UnidadeOrganizacional_Org_Tipo_Ativa"
    ON "UnidadeOrganizacional" ("IdOrganizacao", "Tipo", "Ativa");

CREATE TABLE IF NOT EXISTS "CentroCusto" (
    "IdCentroCusto" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "Codigo" varchar(30) NOT NULL,
    "Descricao" varchar(200) NOT NULL,
    "Ativo" boolean NOT NULL DEFAULT true,
    CONSTRAINT "FK_CentroCusto_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_CentroCusto_Org_Codigo"
    ON "CentroCusto" ("IdOrganizacao", "Codigo");

CREATE INDEX IF NOT EXISTS "IX_CentroCusto_Org_Ativo"
    ON "CentroCusto" ("IdOrganizacao", "Ativo");

CREATE TABLE IF NOT EXISTS "UnidadeCentroCusto" (
    "IdUnidadeOrganizacional" integer NOT NULL,
    "IdCentroCusto" integer NOT NULL,
    "Principal" boolean NOT NULL DEFAULT false,
    CONSTRAINT "PK_UnidadeCentroCusto" PRIMARY KEY ("IdUnidadeOrganizacional", "IdCentroCusto"),
    CONSTRAINT "FK_UnidadeCentroCusto_Unidade" FOREIGN KEY ("IdUnidadeOrganizacional") REFERENCES "UnidadeOrganizacional" ("IdUnidadeOrganizacional"),
    CONSTRAINT "FK_UnidadeCentroCusto_CentroCusto" FOREIGN KEY ("IdCentroCusto") REFERENCES "CentroCusto" ("IdCentroCusto")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_UnidadeCentroCusto_PrincipalPorUnidade"
    ON "UnidadeCentroCusto" ("IdUnidadeOrganizacional")
    WHERE "Principal" = true;

CREATE INDEX IF NOT EXISTS "IX_UnidadeCentroCusto_CentroCusto"
    ON "UnidadeCentroCusto" ("IdCentroCusto", "IdUnidadeOrganizacional");

CREATE TABLE IF NOT EXISTS "Cargo" (
    "IdCargo" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "Codigo" varchar(30) NOT NULL,
    "Nome" varchar(100) NOT NULL,
    "Ativo" boolean NOT NULL DEFAULT true,
    CONSTRAINT "FK_Cargo_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Cargo_Org_Codigo"
    ON "Cargo" ("IdOrganizacao", "Codigo");

CREATE INDEX IF NOT EXISTS "IX_Cargo_Org_Ativo"
    ON "Cargo" ("IdOrganizacao", "Ativo");

CREATE TABLE IF NOT EXISTS "LotacaoUsuario" (
    "IdLotacaoUsuario" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdUsuario" integer NOT NULL,
    "IdUnidadeOrganizacional" integer NOT NULL,
    "IdCargo" integer,
    "DataInicio" timestamp with time zone NOT NULL,
    "DataFim" timestamp with time zone,
    "Principal" boolean NOT NULL DEFAULT false,
    "Ativa" boolean NOT NULL DEFAULT true,
    CONSTRAINT "FK_LotacaoUsuario_Usuario" FOREIGN KEY ("IdUsuario") REFERENCES "Usuario" ("IdUsuario"),
    CONSTRAINT "FK_LotacaoUsuario_UnidadeOrganizacional" FOREIGN KEY ("IdUnidadeOrganizacional") REFERENCES "UnidadeOrganizacional" ("IdUnidadeOrganizacional"),
    CONSTRAINT "FK_LotacaoUsuario_Cargo" FOREIGN KEY ("IdCargo") REFERENCES "Cargo" ("IdCargo"),
    CONSTRAINT "CK_LotacaoUsuario_DataVigencia" CHECK ("DataFim" IS NULL OR "DataFim" >= "DataInicio")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_LotacaoUsuario_PrincipalAtiva"
    ON "LotacaoUsuario" ("IdUsuario", "Principal", "Ativa")
    WHERE "Principal" = true AND "Ativa" = true;

CREATE INDEX IF NOT EXISTS "IX_LotacaoUsuario_Usuario_Ativa"
    ON "LotacaoUsuario" ("IdUsuario", "Ativa");

CREATE INDEX IF NOT EXISTS "IX_LotacaoUsuario_Unidade_Ativa"
    ON "LotacaoUsuario" ("IdUnidadeOrganizacional", "Ativa");

COMMIT;
