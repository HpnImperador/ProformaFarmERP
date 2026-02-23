-- Proforma Farm ERP - Estoque Basico (PostgreSQL)
-- Script: 004_estoque_basico_postgresql.sql
-- Objetivo: criacao idempotente das tabelas e indices do modulo de estoque basico.

BEGIN;

CREATE TABLE IF NOT EXISTS "Produto" (
    "IdProduto" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "Codigo" varchar(50) NOT NULL,
    "Nome" varchar(200) NOT NULL,
    "ControlaLote" boolean NOT NULL DEFAULT false,
    "Ativo" boolean NOT NULL DEFAULT true,
    CONSTRAINT "FK_Produto_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao")
);

CREATE INDEX IF NOT EXISTS "IX_Produto_Org_Codigo"
    ON "Produto" ("IdOrganizacao", "Codigo");

CREATE TABLE IF NOT EXISTS "Lote" (
    "IdLote" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "IdProduto" integer NOT NULL,
    "NumeroLote" varchar(60) NOT NULL,
    "DataFabricacao" timestamp with time zone,
    "DataValidade" timestamp with time zone,
    "Bloqueado" boolean NOT NULL DEFAULT false,
    CONSTRAINT "FK_Lote_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao"),
    CONSTRAINT "FK_Lote_Produto" FOREIGN KEY ("IdProduto") REFERENCES "Produto" ("IdProduto"),
    CONSTRAINT "CK_Lote_DataValidade" CHECK (
        "DataValidade" IS NULL OR "DataFabricacao" IS NULL OR "DataValidade" >= "DataFabricacao"
    )
);

CREATE INDEX IF NOT EXISTS "IX_Lote_Org_Produto_Numero"
    ON "Lote" ("IdOrganizacao", "IdProduto", "NumeroLote");

CREATE TABLE IF NOT EXISTS "Estoque" (
    "IdEstoque" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "IdUnidadeOrganizacional" integer NOT NULL,
    "IdProduto" integer NOT NULL,
    "IdLote" integer,
    "QuantidadeDisponivel" numeric(18,4) NOT NULL DEFAULT 0,
    "QuantidadeReservada" numeric(18,4) NOT NULL DEFAULT 0,
    CONSTRAINT "FK_Estoque_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao"),
    CONSTRAINT "FK_Estoque_Unidade" FOREIGN KEY ("IdUnidadeOrganizacional") REFERENCES "UnidadeOrganizacional" ("IdUnidadeOrganizacional"),
    CONSTRAINT "FK_Estoque_Produto" FOREIGN KEY ("IdProduto") REFERENCES "Produto" ("IdProduto"),
    CONSTRAINT "FK_Estoque_Lote" FOREIGN KEY ("IdLote") REFERENCES "Lote" ("IdLote")
);

CREATE INDEX IF NOT EXISTS "IX_Estoque_Org_Unidade_Produto_Lote"
    ON "Estoque" ("IdOrganizacao", "IdUnidadeOrganizacional", "IdProduto", "IdLote");

CREATE TABLE IF NOT EXISTS "MovimentacaoEstoque" (
    "IdMovimentacaoEstoque" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "IdUnidadeOrganizacional" integer NOT NULL,
    "IdProduto" integer NOT NULL,
    "IdLote" integer,
    "TipoMovimento" varchar(30) NOT NULL,
    "Quantidade" numeric(18,4) NOT NULL,
    "DocumentoReferencia" varchar(80),
    "DataMovimento" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "FK_MovimentacaoEstoque_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao"),
    CONSTRAINT "FK_MovimentacaoEstoque_Unidade" FOREIGN KEY ("IdUnidadeOrganizacional") REFERENCES "UnidadeOrganizacional" ("IdUnidadeOrganizacional"),
    CONSTRAINT "FK_MovimentacaoEstoque_Produto" FOREIGN KEY ("IdProduto") REFERENCES "Produto" ("IdProduto"),
    CONSTRAINT "FK_MovimentacaoEstoque_Lote" FOREIGN KEY ("IdLote") REFERENCES "Lote" ("IdLote")
);

CREATE INDEX IF NOT EXISTS "IX_MovimentacaoEstoque_Org_Unidade_Data"
    ON "MovimentacaoEstoque" ("IdOrganizacao", "IdUnidadeOrganizacional", "DataMovimento");

CREATE TABLE IF NOT EXISTS "ReservaEstoque" (
    "IdReservaEstoque" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdOrganizacao" integer NOT NULL,
    "IdUnidadeOrganizacional" integer NOT NULL,
    "IdProduto" integer NOT NULL,
    "IdLote" integer,
    "Quantidade" numeric(18,4) NOT NULL,
    "ExpiraEmUtc" timestamp with time zone NOT NULL,
    "Status" varchar(20) NOT NULL,
    "DocumentoReferencia" varchar(80),
    CONSTRAINT "FK_ReservaEstoque_Organizacao" FOREIGN KEY ("IdOrganizacao") REFERENCES "Organizacao" ("IdOrganizacao"),
    CONSTRAINT "FK_ReservaEstoque_Unidade" FOREIGN KEY ("IdUnidadeOrganizacional") REFERENCES "UnidadeOrganizacional" ("IdUnidadeOrganizacional"),
    CONSTRAINT "FK_ReservaEstoque_Produto" FOREIGN KEY ("IdProduto") REFERENCES "Produto" ("IdProduto"),
    CONSTRAINT "FK_ReservaEstoque_Lote" FOREIGN KEY ("IdLote") REFERENCES "Lote" ("IdLote")
);

CREATE INDEX IF NOT EXISTS "IX_ReservaEstoque_Org_Status_Expira"
    ON "ReservaEstoque" ("IdOrganizacao", "Status", "ExpiraEmUtc");

COMMIT;
