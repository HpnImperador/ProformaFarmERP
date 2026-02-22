/*
  Proforma Farm ERP - Estoque Basico
  Script: 004_estoque_basico.sql
  Banco alvo: SQL Server
  Objetivo: Criacao/adaptacao idempotente de tabelas e indices do modulo de estoque basico.
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

/* =========================================================
   PRODUTO (cria ou adapta tabela legada)
   ========================================================= */
IF OBJECT_ID(N'dbo.Produto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Produto
    (
        IdProduto INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        Codigo NVARCHAR(50) NOT NULL,
        Nome NVARCHAR(200) NOT NULL,
        ControlaLote BIT NOT NULL CONSTRAINT DF_Produto_ControlaLote DEFAULT (0),
        Ativo BIT NOT NULL CONSTRAINT DF_Produto_Ativo DEFAULT (1),
        CONSTRAINT PK_Produto PRIMARY KEY CLUSTERED (IdProduto),
        CONSTRAINT FK_Produto_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION
    );
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.Produto','IdOrganizacao') IS NULL
        ALTER TABLE dbo.Produto ADD IdOrganizacao INT NULL;

    IF COL_LENGTH('dbo.Produto','Codigo') IS NULL
        ALTER TABLE dbo.Produto ADD Codigo NVARCHAR(50) NULL;

    IF COL_LENGTH('dbo.Produto','Nome') IS NULL
        ALTER TABLE dbo.Produto ADD Nome NVARCHAR(200) NULL;

    IF COL_LENGTH('dbo.Produto','ControlaLote') IS NULL
        ALTER TABLE dbo.Produto ADD ControlaLote BIT NULL;

    IF COL_LENGTH('dbo.Produto','Ativo') IS NULL
        ALTER TABLE dbo.Produto ADD Ativo BIT NULL;

    IF COL_LENGTH('dbo.Produto','IdOrganizacao') IS NOT NULL
        EXEC(N'UPDATE dbo.Produto SET IdOrganizacao = ISNULL(IdOrganizacao, 1);');

    IF COL_LENGTH('dbo.Produto','CodigoBarras') IS NOT NULL
        EXEC(N'
        UPDATE dbo.Produto
           SET Codigo = COALESCE(NULLIF(Codigo, ''''), LEFT(CONVERT(NVARCHAR(50), CodigoBarras), 50))
         WHERE Codigo IS NULL OR Codigo = '''';
        ');

    IF COL_LENGTH('dbo.Produto','Descricao') IS NOT NULL
        EXEC(N'
        UPDATE dbo.Produto
           SET Nome = COALESCE(NULLIF(Nome, ''''), LEFT(CONVERT(NVARCHAR(200), Descricao), 200))
         WHERE Nome IS NULL OR Nome = '''';
        ');

    IF COL_LENGTH('dbo.Produto','Controlado') IS NOT NULL
        EXEC(N'
        UPDATE dbo.Produto
           SET ControlaLote = ISNULL(ControlaLote, CASE WHEN Controlado = 1 THEN 1 ELSE 0 END)
         WHERE ControlaLote IS NULL;
        ');

    IF COL_LENGTH('dbo.Produto','ControlaLote') IS NOT NULL
        EXEC(N'UPDATE dbo.Produto SET ControlaLote = ISNULL(ControlaLote, 0);');

    IF COL_LENGTH('dbo.Produto','Ativo') IS NOT NULL
        EXEC(N'UPDATE dbo.Produto SET Ativo = ISNULL(Ativo, 1);');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Produto') AND name = N'IX_Produto_Org_Codigo')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Produto_Org_Codigo
        ON dbo.Produto (IdOrganizacao, Codigo);
END;

/* =========================================================
   LOTE
   ========================================================= */
IF OBJECT_ID(N'dbo.Lote', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Lote
    (
        IdLote INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        IdProduto INT NOT NULL,
        NumeroLote NVARCHAR(60) NOT NULL,
        DataFabricacao DATETIME2(0) NULL,
        DataValidade DATETIME2(0) NULL,
        Bloqueado BIT NOT NULL CONSTRAINT DF_Lote_Bloqueado DEFAULT (0),
        CONSTRAINT PK_Lote PRIMARY KEY CLUSTERED (IdLote),
        CONSTRAINT FK_Lote_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION,
        CONSTRAINT FK_Lote_Produto
            FOREIGN KEY (IdProduto) REFERENCES dbo.Produto (IdProduto) ON DELETE NO ACTION,
        CONSTRAINT CK_Lote_DataValidade CHECK (DataValidade IS NULL OR DataFabricacao IS NULL OR DataValidade >= DataFabricacao)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Lote') AND name = N'IX_Lote_Org_Produto_Numero')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Lote_Org_Produto_Numero
        ON dbo.Lote (IdOrganizacao, IdProduto, NumeroLote);
END;

/* =========================================================
   ESTOQUE (cria ou adapta tabela legada)
   ========================================================= */
IF OBJECT_ID(N'dbo.Estoque', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Estoque
    (
        IdEstoque INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        IdUnidadeOrganizacional INT NOT NULL,
        IdProduto INT NOT NULL,
        IdLote INT NULL,
        QuantidadeDisponivel DECIMAL(18,4) NOT NULL CONSTRAINT DF_Estoque_QtdDisp DEFAULT (0),
        QuantidadeReservada DECIMAL(18,4) NOT NULL CONSTRAINT DF_Estoque_QtdRes DEFAULT (0),
        CONSTRAINT PK_Estoque PRIMARY KEY CLUSTERED (IdEstoque),
        CONSTRAINT FK_Estoque_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION,
        CONSTRAINT FK_Estoque_Unidade
            FOREIGN KEY (IdUnidadeOrganizacional) REFERENCES dbo.UnidadeOrganizacional (IdUnidadeOrganizacional) ON DELETE NO ACTION,
        CONSTRAINT FK_Estoque_Produto
            FOREIGN KEY (IdProduto) REFERENCES dbo.Produto (IdProduto) ON DELETE NO ACTION,
        CONSTRAINT FK_Estoque_Lote
            FOREIGN KEY (IdLote) REFERENCES dbo.Lote (IdLote) ON DELETE NO ACTION
    );
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.Estoque','IdOrganizacao') IS NULL
        ALTER TABLE dbo.Estoque ADD IdOrganizacao INT NULL;

    IF COL_LENGTH('dbo.Estoque','IdUnidadeOrganizacional') IS NULL
        ALTER TABLE dbo.Estoque ADD IdUnidadeOrganizacional INT NULL;

    IF COL_LENGTH('dbo.Estoque','IdLote') IS NULL
        ALTER TABLE dbo.Estoque ADD IdLote INT NULL;

    IF COL_LENGTH('dbo.Estoque','QuantidadeDisponivel') IS NULL
        ALTER TABLE dbo.Estoque ADD QuantidadeDisponivel DECIMAL(18,4) NULL;

    IF COL_LENGTH('dbo.Estoque','QuantidadeReservada') IS NULL
        ALTER TABLE dbo.Estoque ADD QuantidadeReservada DECIMAL(18,4) NULL;

    IF COL_LENGTH('dbo.Estoque','IdOrganizacao') IS NOT NULL
        EXEC(N'UPDATE dbo.Estoque SET IdOrganizacao = ISNULL(IdOrganizacao, 1);');

    IF COL_LENGTH('dbo.Estoque','IdFilial') IS NOT NULL
        EXEC(N'
        UPDATE dbo.Estoque
           SET IdUnidadeOrganizacional = ISNULL(IdUnidadeOrganizacional, IdFilial)
         WHERE IdUnidadeOrganizacional IS NULL;
        ');

    IF COL_LENGTH('dbo.Estoque','Quantidade') IS NOT NULL
        EXEC(N'
        UPDATE dbo.Estoque
           SET QuantidadeDisponivel = ISNULL(QuantidadeDisponivel, CONVERT(DECIMAL(18,4), Quantidade))
         WHERE QuantidadeDisponivel IS NULL;
        ');

    IF COL_LENGTH('dbo.Estoque','QuantidadeDisponivel') IS NOT NULL
        EXEC(N'UPDATE dbo.Estoque SET QuantidadeDisponivel = ISNULL(QuantidadeDisponivel, 0);');

    IF COL_LENGTH('dbo.Estoque','QuantidadeReservada') IS NOT NULL
        EXEC(N'UPDATE dbo.Estoque SET QuantidadeReservada = ISNULL(QuantidadeReservada, 0);');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Estoque') AND name = N'IX_Estoque_Org_Unidade_Produto_Lote')
BEGIN
    CREATE NONCLUSTERED INDEX IX_Estoque_Org_Unidade_Produto_Lote
        ON dbo.Estoque (IdOrganizacao, IdUnidadeOrganizacional, IdProduto, IdLote);
END;

/* =========================================================
   MOVIMENTACAO ESTOQUE
   ========================================================= */
IF OBJECT_ID(N'dbo.MovimentacaoEstoque', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MovimentacaoEstoque
    (
        IdMovimentacaoEstoque INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        IdUnidadeOrganizacional INT NOT NULL,
        IdProduto INT NOT NULL,
        IdLote INT NULL,
        TipoMovimento NVARCHAR(30) NOT NULL,
        Quantidade DECIMAL(18,4) NOT NULL,
        DocumentoReferencia NVARCHAR(80) NULL,
        DataMovimento DATETIME2(0) NOT NULL CONSTRAINT DF_MovimentacaoEstoque_Data DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MovimentacaoEstoque PRIMARY KEY CLUSTERED (IdMovimentacaoEstoque),
        CONSTRAINT FK_MovimentacaoEstoque_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION,
        CONSTRAINT FK_MovimentacaoEstoque_Unidade
            FOREIGN KEY (IdUnidadeOrganizacional) REFERENCES dbo.UnidadeOrganizacional (IdUnidadeOrganizacional) ON DELETE NO ACTION,
        CONSTRAINT FK_MovimentacaoEstoque_Produto
            FOREIGN KEY (IdProduto) REFERENCES dbo.Produto (IdProduto) ON DELETE NO ACTION,
        CONSTRAINT FK_MovimentacaoEstoque_Lote
            FOREIGN KEY (IdLote) REFERENCES dbo.Lote (IdLote) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MovimentacaoEstoque') AND name = N'IX_MovimentacaoEstoque_Org_Unidade_Data')
BEGIN
    CREATE NONCLUSTERED INDEX IX_MovimentacaoEstoque_Org_Unidade_Data
        ON dbo.MovimentacaoEstoque (IdOrganizacao, IdUnidadeOrganizacional, DataMovimento)
        INCLUDE (IdProduto, IdLote, TipoMovimento, Quantidade, DocumentoReferencia);
END;

/* =========================================================
   RESERVA ESTOQUE
   ========================================================= */
IF OBJECT_ID(N'dbo.ReservaEstoque', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReservaEstoque
    (
        IdReservaEstoque INT IDENTITY(1,1) NOT NULL,
        IdOrganizacao INT NOT NULL,
        IdUnidadeOrganizacional INT NOT NULL,
        IdProduto INT NOT NULL,
        IdLote INT NULL,
        Quantidade DECIMAL(18,4) NOT NULL,
        ExpiraEmUtc DATETIME2(0) NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        DocumentoReferencia NVARCHAR(80) NULL,
        CONSTRAINT PK_ReservaEstoque PRIMARY KEY CLUSTERED (IdReservaEstoque),
        CONSTRAINT FK_ReservaEstoque_Organizacao
            FOREIGN KEY (IdOrganizacao) REFERENCES dbo.Organizacao (IdOrganizacao) ON DELETE NO ACTION,
        CONSTRAINT FK_ReservaEstoque_Unidade
            FOREIGN KEY (IdUnidadeOrganizacional) REFERENCES dbo.UnidadeOrganizacional (IdUnidadeOrganizacional) ON DELETE NO ACTION,
        CONSTRAINT FK_ReservaEstoque_Produto
            FOREIGN KEY (IdProduto) REFERENCES dbo.Produto (IdProduto) ON DELETE NO ACTION,
        CONSTRAINT FK_ReservaEstoque_Lote
            FOREIGN KEY (IdLote) REFERENCES dbo.Lote (IdLote) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ReservaEstoque') AND name = N'IX_ReservaEstoque_Org_Status_Expira')
BEGIN
    CREATE NONCLUSTERED INDEX IX_ReservaEstoque_Org_Status_Expira
        ON dbo.ReservaEstoque (IdOrganizacao, Status, ExpiraEmUtc)
        INCLUDE (IdUnidadeOrganizacional, IdProduto, IdLote, Quantidade, DocumentoReferencia);
END;

COMMIT TRANSACTION;
GO
