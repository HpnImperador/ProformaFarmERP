-- Proforma Farm ERP - Otimizacao OrgContext (PostgreSQL)
-- Script: 003_idx_lotacaousuario_orgcontext_postgresql.sql

CREATE INDEX IF NOT EXISTS "IX_LotacaoUsuario_Usuario_Ativa_Principal"
    ON "LotacaoUsuario" ("IdUsuario", "Ativa", "Principal", "IdUnidadeOrganizacional");
