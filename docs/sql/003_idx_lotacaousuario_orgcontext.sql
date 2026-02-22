/*
  Proforma Farm ERP - Otimizacao OrgContext
  Script: 003_idx_lotacaousuario_orgcontext.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.LotacaoUsuario')
      AND name = N'IX_LotacaoUsuario_Usuario_Ativa_Principal'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_LotacaoUsuario_Usuario_Ativa_Principal
        ON dbo.LotacaoUsuario (IdUsuario, Ativa, Principal)
        INCLUDE (IdUnidadeOrganizacional);
END;
GO
