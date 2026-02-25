/*
  Proforma Farm ERP - Auth Base (PostgreSQL)
  Script: 000_auth_base_postgresql.sql
  Objetivo: criar estrutura mínima de autenticação para suportar OrgContext, seed e testes.
*/

BEGIN;

CREATE TABLE IF NOT EXISTS "Usuario" (
    "IdUsuario" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Nome" character varying(200) NOT NULL,
    "Login" character varying(80) NOT NULL,
    "SenhaHash" character varying(512) NOT NULL,
    "SenhaSalt" character varying(256),
    "Ativo" boolean NOT NULL DEFAULT true,
    "DataCriacao" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Usuario_Login"
    ON "Usuario" ("Login");

CREATE TABLE IF NOT EXISTS "Perfil" (
    "IdPerfil" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Nome" character varying(80) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Perfil_Nome"
    ON "Perfil" ("Nome");

CREATE TABLE IF NOT EXISTS "UsuarioPerfil" (
    "IdUsuario" integer NOT NULL,
    "IdPerfil" integer NOT NULL,
    CONSTRAINT "PK_UsuarioPerfil" PRIMARY KEY ("IdUsuario", "IdPerfil"),
    CONSTRAINT "FK_UsuarioPerfil_Usuario" FOREIGN KEY ("IdUsuario") REFERENCES "Usuario" ("IdUsuario"),
    CONSTRAINT "FK_UsuarioPerfil_Perfil" FOREIGN KEY ("IdPerfil") REFERENCES "Perfil" ("IdPerfil")
);

CREATE TABLE IF NOT EXISTS "RefreshToken" (
    "IdRefreshToken" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "IdUsuario" integer NOT NULL,
    "TokenHash" character varying(300) NOT NULL,
    "CriadoEmUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ExpiraEmUtc" timestamp with time zone NOT NULL,
    "RevogadoEmUtc" timestamp with time zone,
    "CriadoPorIp" character varying(100),
    "RevogadoPorIp" character varying(100),
    "SubstituidoPorHash" character varying(300),
    CONSTRAINT "FK_RefreshToken_Usuario" FOREIGN KEY ("IdUsuario") REFERENCES "Usuario" ("IdUsuario")
);

CREATE INDEX IF NOT EXISTS "IX_RefreshToken_IdUsuario"
    ON "RefreshToken" ("IdUsuario");

CREATE UNIQUE INDEX IF NOT EXISTS "UX_RefreshToken_TokenHash"
    ON "RefreshToken" ("TokenHash");

COMMIT;
