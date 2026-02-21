/*
  Proforma Farm ERP - Estrutura Organizacional
  Script: 001_estrutura_organizacional.sql
  Banco alvo: SQL Server
  Objetivo: Criacao idempotente das tabelas e indices da estrutura organizacional.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* =========================================================
   ORGANIZACAO
   ========================================================= */
IF OBJECT_ID(N'dbo.Organizacao', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Organizacao
    (
        IdOrganizacao INT IDENTITY(1,1) NOT NULL,
        RazaoSocial NVARCHAR(200) NOT NULL,
        NomeFantasia NVARCHAR(200) NULL,
        Cnpj CHAR(14) NOT NULL,
        Ativa BIT NOT NULL CONSTRAINT DF_Organizacao_Ativa DEFAULT (1),
        DataCriacao DATETIME2(0) NOT NULL CONSTRAINT DF_Organizacao_DataCriacao DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Organizacao PRIMARY KEY CLUSTERED (IdOrganizacao),
        CONSTRAINT CK_Organizacao_Cnpj_Formato CHECK (Cnpj NOT LIKE '%[^0-9]%')
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Organizacao') AND name = N'UX_Organizacao_Cnpj')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Organizacao_Cnpj
        ON dbo.Organizacao (Cnpj);
END;

/* =========================================================
   UNIDADE ORGANIZACIONAL
   ========================================================= */
IF OBJECT_ID(N'dbo.UnidadeOrganizacional', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UnidadeOrganizacional
    (
        IdUnidadeOrganizacional INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        IdUnidadePai INT NULL,
        Tipo NVARCHAR(40) NOT NULL,
        Codigo NVARCHAR(30) NOT NULL,
        Nome NVARCHAR(200) NOT NULL,
        Ativa BIT NOT NULL CONSTRAINT DF_UnidadeOrganizacional_Ativa DEFAULT (1),
        DataInicio DATETIME2(0) NOT NULL,
        DataFim DATETIME2(0) NULL,
        CONSTRAINT PK_UnidadeOrganizacional PRIMARY KEY CLUSTERED (IdUnidadeOrganizacional),
        CONSTRAINT FK_UnidadeOrganizacional_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION,
        CONSTRAINT FK_UnidadeOrganizacional_UnidadePai
            FOREIGN KEY (IdUnidadePai) REFERENCES dbo.UnidadeOrganizacional (IdUnidadeOrganizacional) ON DELETE NO ACTION,
        CONSTRAINT CK_UnidadeOrganizacional_DataVigencia CHECK (DataFim IS NULL OR DataFim >= DataInicio),
        CONSTRAINT CK_UnidadeOrganizacional_SemAutoReferencia CHECK (IdUnidadePai IS NULL OR IdUnidadePai <> IdUnidadeOrganizacional)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UnidadeOrganizacional') AND name = N'UX_UnidadeOrganizacional_Org_Codigo')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_UnidadeOrganizacional_Org_Codigo
        ON dbo.UnidadeOrganizacional (IdOrganizacao, Codigo);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UnidadeOrganizacional') AND name = N'IX_UnidadeOrganizacional_Org_Pai_Ativa')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UnidadeOrganizacional_Org_Pai_Ativa
        ON dbo.UnidadeOrganizacional (IdOrganizacao, IdUnidadePai, Ativa)
        INCLUDE (Tipo, Nome, Codigo);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UnidadeOrganizacional') AND name = N'IX_UnidadeOrganizacional_Org_Tipo_Ativa')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UnidadeOrganizacional_Org_Tipo_Ativa
        ON dbo.UnidadeOrganizacional (IdOrganizacao, Tipo, Ativa)
        INCLUDE (Nome, Codigo, IdUnidadePai);
END;

/* =========================================================
   CENTRO DE CUSTO
   ========================================================= */
IF OBJECT_ID(N'dbo.CentroCusto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CentroCusto
    (
        IdCentroCusto INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        Codigo NVARCHAR(30) NOT NULL,
        Descricao NVARCHAR(200) NOT NULL,
        Ativo BIT NOT NULL CONSTRAINT DF_CentroCusto_Ativo DEFAULT (1),
        CONSTRAINT PK_CentroCusto PRIMARY KEY CLUSTERED (IdCentroCusto),
        CONSTRAINT FK_CentroCusto_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CentroCusto') AND name = N'UX_CentroCusto_Org_Codigo')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_CentroCusto_Org_Codigo
        ON dbo.CentroCusto (IdOrganizacao, Codigo);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CentroCusto') AND name = N'IX_CentroCusto_Org_Ativo')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CentroCusto_Org_Ativo
        ON dbo.CentroCusto (IdOrganizacao, Ativo)
        INCLUDE (Codigo, Descricao);
END;

/* =========================================================
   UNIDADE x CENTRO DE CUSTO
   ========================================================= */
IF OBJECT_ID(N'dbo.UnidadeCentroCusto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UnidadeCentroCusto
    (
        IdUnidadeOrganizacional INT NOT NULL,
        IdCentroCusto INT NOT NULL,
        Principal BIT NOT NULL CONSTRAINT DF_UnidadeCentroCusto_Principal DEFAULT (0),
        CONSTRAINT PK_UnidadeCentroCusto PRIMARY KEY CLUSTERED (IdUnidadeOrganizacional, IdCentroCusto),
        CONSTRAINT FK_UnidadeCentroCusto_Unidade
            FOREIGN KEY (IdUnidadeOrganizacional) REFERENCES dbo.UnidadeOrganizacional (IdUnidadeOrganizacional) ON DELETE NO ACTION,
        CONSTRAINT FK_UnidadeCentroCusto_CentroCusto
            FOREIGN KEY (IdCentroCusto) REFERENCES dbo.CentroCusto (IdCentroCusto) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UnidadeCentroCusto') AND name = N'UX_UnidadeCentroCusto_PrincipalPorUnidade')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_UnidadeCentroCusto_PrincipalPorUnidade
        ON dbo.UnidadeCentroCusto (IdUnidadeOrganizacional)
        WHERE Principal = 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UnidadeCentroCusto') AND name = N'IX_UnidadeCentroCusto_CentroCusto')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UnidadeCentroCusto_CentroCusto
        ON dbo.UnidadeCentroCusto (IdCentroCusto, IdUnidadeOrganizacional)
        INCLUDE (Principal);
END;

/* =========================================================
   CARGO
   ========================================================= */
IF OBJECT_ID(N'dbo.Cargo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cargo
    (
        IdCargo INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        Codigo NVARCHAR(30) NOT NULL,
        Nome NVARCHAR(100) NOT NULL,
        Ativo BIT NOT NULL CONSTRAINT DF_Cargo_Ativo DEFAULT (1),
        CONSTRAINT PK_Cargo PRIMARY KEY CLUSTERED (IdCargo),
        CONSTRAINT FK_Cargo_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Cargo') AND name = N'UX_Cargo_Org_Codigo')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Cargo_Org_Codigo
        ON dbo.Cargo (IdOrganizacao, Codigo);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Cargo') AND name = N'IX_Cargo_Org_Ativo')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Cargo_Org_Ativo
        ON dbo.Cargo (IdOrganizacao, Ativo)
        INCLUDE (Codigo, Nome);
END;

/* =========================================================
   LOTACAO USUARIO
   ========================================================= */
IF OBJECT_ID(N'dbo.LotacaoUsuario', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LotacaoUsuario
    (
        IdLotacaoUsuario INT IDENTITY(1,1) NOT NULL,
        IdUsuario INT NOT NULL,
        IdUnidadeOrganizacional INT NOT NULL,
        IdCargo INT NULL,
        DataInicio DATETIME2(0) NOT NULL,
        DataFim DATETIME2(0) NULL,
        Principal BIT NOT NULL CONSTRAINT DF_LotacaoUsuario_Principal DEFAULT (0),
        Ativa BIT NOT NULL CONSTRAINT DF_LotacaoUsuario_Ativa DEFAULT (1),
        CONSTRAINT PK_LotacaoUsuario PRIMARY KEY CLUSTERED (IdLotacaoUsuario),
        CONSTRAINT FK_LotacaoUsuario_Usuario
            FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuario (IdUsuario) ON DELETE NO ACTION,
        CONSTRAINT FK_LotacaoUsuario_UnidadeOrganizacional
            FOREIGN KEY (IdUnidadeOrganizacional) REFERENCES dbo.UnidadeOrganizacional (IdUnidadeOrganizacional) ON DELETE NO ACTION,
        CONSTRAINT FK_LotacaoUsuario_Cargo
            FOREIGN KEY (IdCargo) REFERENCES dbo.Cargo (IdCargo) ON DELETE NO ACTION,
        CONSTRAINT CK_LotacaoUsuario_DataVigencia CHECK (DataFim IS NULL OR DataFim >= DataInicio)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LotacaoUsuario') AND name = N'UX_LotacaoUsuario_PrincipalAtiva')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_LotacaoUsuario_PrincipalAtiva
        ON dbo.LotacaoUsuario (IdUsuario, Principal, Ativa)
        WHERE Principal = 1 AND Ativa = 1;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LotacaoUsuario') AND name = N'IX_LotacaoUsuario_Usuario_Ativa')
BEGIN
    CREATE NONCLUSTERED INDEX IX_LotacaoUsuario_Usuario_Ativa
        ON dbo.LotacaoUsuario (IdUsuario, Ativa)
        INCLUDE (IdUnidadeOrganizacional, IdCargo, Principal, DataInicio, DataFim);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LotacaoUsuario') AND name = N'IX_LotacaoUsuario_Unidade_Ativa')
BEGIN
    CREATE NONCLUSTERED INDEX IX_LotacaoUsuario_Unidade_Ativa
        ON dbo.LotacaoUsuario (IdUnidadeOrganizacional, Ativa)
        INCLUDE (IdUsuario, IdCargo, Principal, DataInicio, DataFim);
END;

COMMIT TRANSACTION;
GO
