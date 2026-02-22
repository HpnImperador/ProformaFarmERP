/*
  Proforma Farm ERP - Seed minimo Estrutura Organizacional
  Script: 002_seed_estrutura_organizacional.sql
  Banco alvo: SQL Server
  Objetivo: Popular dados minimos para homologacao de estrutura e lotacao.
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

BEGIN TRANSACTION;

DECLARE @IdOrganizacao INT;
DECLARE @IdMatriz INT;
DECLARE @IdFilial INT;
DECLARE @IdCentroCusto INT;
DECLARE @IdCargo INT;
DECLARE @IdUsuario INT;

/* =========================================================
   ORGANIZACAO
   ========================================================= */
SELECT @IdOrganizacao = IdOrganizacao
FROM dbo.Organizacao
WHERE Cnpj = '12345678000199';

IF @IdOrganizacao IS NULL
BEGIN
    INSERT INTO dbo.Organizacao (RazaoSocial, NomeFantasia, Cnpj, Ativa, DataCriacao)
    VALUES (N'Proforma Farm LTDA', N'Proforma Farm', '12345678000199', 1, SYSUTCDATETIME());

    SET @IdOrganizacao = SCOPE_IDENTITY();
END;

/* =========================================================
   UNIDADES
   ========================================================= */
SELECT @IdMatriz = IdUnidadeOrganizacional
FROM dbo.UnidadeOrganizacional
WHERE IdOrganizacao = @IdOrganizacao AND Codigo = 'MATRIZ';

IF @IdMatriz IS NULL
BEGIN
    INSERT INTO dbo.UnidadeOrganizacional
    (
        IdOrganizacao, IdUnidadePai, Tipo, Codigo, Nome, Ativa, DataInicio, DataFim
    )
    VALUES
    (
        @IdOrganizacao, NULL, N'Matriz', 'MATRIZ', N'Matriz Central', 1, SYSUTCDATETIME(), NULL
    );

    SET @IdMatriz = SCOPE_IDENTITY();
END;

SELECT @IdFilial = IdUnidadeOrganizacional
FROM dbo.UnidadeOrganizacional
WHERE IdOrganizacao = @IdOrganizacao AND Codigo = 'FILIAL-001';

IF @IdFilial IS NULL
BEGIN
    INSERT INTO dbo.UnidadeOrganizacional
    (
        IdOrganizacao, IdUnidadePai, Tipo, Codigo, Nome, Ativa, DataInicio, DataFim
    )
    VALUES
    (
        @IdOrganizacao, @IdMatriz, N'Filial', 'FILIAL-001', N'Filial 001', 1, SYSUTCDATETIME(), NULL
    );

    SET @IdFilial = SCOPE_IDENTITY();
END;

/* =========================================================
   CENTRO DE CUSTO + VINCULO
   ========================================================= */
SELECT @IdCentroCusto = IdCentroCusto
FROM dbo.CentroCusto
WHERE IdOrganizacao = @IdOrganizacao AND Codigo = 'ADM-GERAL';

IF @IdCentroCusto IS NULL
BEGIN
    INSERT INTO dbo.CentroCusto (IdOrganizacao, Codigo, Descricao, Ativo)
    VALUES (@IdOrganizacao, 'ADM-GERAL', N'Administracao Geral', 1);

    SET @IdCentroCusto = SCOPE_IDENTITY();
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.UnidadeCentroCusto
    WHERE IdUnidadeOrganizacional = @IdMatriz
      AND IdCentroCusto = @IdCentroCusto
)
BEGIN
    INSERT INTO dbo.UnidadeCentroCusto (IdUnidadeOrganizacional, IdCentroCusto, Principal)
    VALUES (@IdMatriz, @IdCentroCusto, 1);
END;

/* =========================================================
   CARGO
   ========================================================= */
SELECT @IdCargo = IdCargo
FROM dbo.Cargo
WHERE IdOrganizacao = @IdOrganizacao AND Codigo = 'GERENTE';

IF @IdCargo IS NULL
BEGIN
    INSERT INTO dbo.Cargo (IdOrganizacao, Codigo, Nome, Ativo)
    VALUES (@IdOrganizacao, 'GERENTE', N'Gerente', 1);

    SET @IdCargo = SCOPE_IDENTITY();
END;

/* =========================================================
   LOTACAO USUARIO (usa um usuario ativo existente)
   ========================================================= */
SELECT TOP (1) @IdUsuario = IdUsuario
FROM dbo.Usuario
WHERE Ativo = 1
ORDER BY IdUsuario;

IF @IdUsuario IS NOT NULL
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.LotacaoUsuario
        WHERE IdUsuario = @IdUsuario
          AND Principal = 1
          AND Ativa = 1
    )
    BEGIN
        INSERT INTO dbo.LotacaoUsuario
        (
            IdUsuario, IdUnidadeOrganizacional, IdCargo, DataInicio, DataFim, Principal, Ativa
        )
        VALUES
        (
            @IdUsuario, @IdMatriz, @IdCargo, SYSUTCDATETIME(), NULL, 1, 1
        );
    END;
END;

COMMIT TRANSACTION;
GO
