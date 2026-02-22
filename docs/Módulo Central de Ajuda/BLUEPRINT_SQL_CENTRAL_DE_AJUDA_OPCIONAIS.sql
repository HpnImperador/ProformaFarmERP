/* BLUEPRINT SQL (OPCIONAIS) — CENTRAL DE AJUDA (HELP CENTER)
   Banco: SQL Server
   Este script adiciona recursos avançados para a Central de Ajuda.
   Pré-requisito: BLUEPRINT_SQL_CENTRAL_DE_AJUDA_MVP_MINIMAL.sql já aplicado.
*/

/* A) Tags */
IF OBJECT_ID('dbo.HelpTag', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpTag
    (
        IdHelpTag            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpTag PRIMARY KEY,
        Nome                 NVARCHAR(80) NOT NULL,
        Slug                 NVARCHAR(100) NOT NULL
    );

    CREATE UNIQUE INDEX UX_HelpTag_Slug ON dbo.HelpTag (Slug);
END
GO

IF OBJECT_ID('dbo.HelpArtigoTag', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpArtigoTag
    (
        IdHelpArtigo         INT NOT NULL,
        IdHelpTag            INT NOT NULL,
        CONSTRAINT PK_HelpArtigoTag PRIMARY KEY (IdHelpArtigo, IdHelpTag)
    );

    ALTER TABLE dbo.HelpArtigoTag
        ADD CONSTRAINT FK_HelpArtigoTag_Artigo
        FOREIGN KEY (IdHelpArtigo) REFERENCES dbo.HelpArtigo(IdHelpArtigo)
        ON DELETE CASCADE;

    ALTER TABLE dbo.HelpArtigoTag
        ADD CONSTRAINT FK_HelpArtigoTag_Tag
        FOREIGN KEY (IdHelpTag) REFERENCES dbo.HelpTag(IdHelpTag)
        ON DELETE CASCADE;
END
GO

/* B) Contexto (módulo/tela/feature) */
IF OBJECT_ID('dbo.HelpArtigoContexto', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpArtigoContexto
    (
        IdHelpArtigoContexto INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpArtigoContexto PRIMARY KEY,
        IdHelpArtigo         INT NOT NULL,
        Modulo               NVARCHAR(80) NOT NULL,
        Tela                 NVARCHAR(120) NULL,
        FeatureFlag          NVARCHAR(120) NULL,
        Ordem                INT NOT NULL CONSTRAINT DF_HelpArtigoContexto_Ordem DEFAULT(0)
    );

    ALTER TABLE dbo.HelpArtigoContexto
        ADD CONSTRAINT FK_HelpArtigoContexto_Artigo
        FOREIGN KEY (IdHelpArtigo) REFERENCES dbo.HelpArtigo(IdHelpArtigo)
        ON DELETE CASCADE;

    CREATE INDEX IX_HelpArtigoContexto_Modulo_Tela ON dbo.HelpArtigoContexto (Modulo, Tela);
END
GO

/* C) FAQ */
IF OBJECT_ID('dbo.HelpFaq', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpFaq
    (
        IdHelpFaq            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpFaq PRIMARY KEY,
        IdHelpCategoria      INT NOT NULL,
        Pergunta             NVARCHAR(250) NOT NULL,
        RespostaMarkdown     NVARCHAR(MAX) NOT NULL,
        RespostaHtml         NVARCHAR(MAX) NULL,
        Ordem                INT NOT NULL CONSTRAINT DF_HelpFaq_Ordem DEFAULT(0),
        Ativa                BIT NOT NULL CONSTRAINT DF_HelpFaq_Ativa DEFAULT(1),
        CriadoEm             DATETIME2(0) NOT NULL CONSTRAINT DF_HelpFaq_CriadoEm DEFAULT(SYSUTCDATETIME()),
        CriadoPorUsuarioId   INT NOT NULL
    );

    ALTER TABLE dbo.HelpFaq
        ADD CONSTRAINT FK_HelpFaq_Categoria
        FOREIGN KEY (IdHelpCategoria) REFERENCES dbo.HelpCategoria(IdHelpCategoria)
        ON DELETE NO ACTION;

    CREATE INDEX IX_HelpFaq_Categoria_Ativa ON dbo.HelpFaq (IdHelpCategoria, Ativa);
END
GO

/* D) FULLTEXT (opcional; requer catálogo e permissões) */
-- CREATE FULLTEXT CATALOG HelpCatalog AS DEFAULT;
-- GO
-- CREATE FULLTEXT INDEX ON dbo.HelpArtigoVersao(ConteudoMarkdown LANGUAGE 1046, ConteudoHtml LANGUAGE 1046)
-- KEY INDEX PK_HelpArtigoVersao
-- WITH STOPLIST = SYSTEM;
-- GO

/* E) IA (fase 2): embeddings + log de respostas */
IF OBJECT_ID('dbo.HelpArtigoEmbedding', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpArtigoEmbedding
    (
        IdHelpArtigoEmbedding BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpArtigoEmbedding PRIMARY KEY,
        IdHelpArtigo          INT NOT NULL,
        IdHelpArtigoVersao    INT NOT NULL,
        Modelo                NVARCHAR(80) NOT NULL,
        Embedding             VARBINARY(MAX) NOT NULL,
        CriadoEm              DATETIME2(0) NOT NULL CONSTRAINT DF_HelpArtigoEmbedding_CriadoEm DEFAULT(SYSUTCDATETIME())
    );

    ALTER TABLE dbo.HelpArtigoEmbedding
        ADD CONSTRAINT FK_HelpArtigoEmbedding_Artigo
        FOREIGN KEY (IdHelpArtigo) REFERENCES dbo.HelpArtigo(IdHelpArtigo)
        ON DELETE CASCADE;

    CREATE UNIQUE INDEX UX_HelpArtigoEmbedding_ArtigoVersao_Modelo
        ON dbo.HelpArtigoEmbedding (IdHelpArtigoVersao, Modelo);
END
GO

IF OBJECT_ID('dbo.HelpAIAnswerLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpAIAnswerLog
    (
        IdHelpAIAnswerLog    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpAIAnswerLog PRIMARY KEY,
        UsuarioId            INT NULL,
        Pergunta             NVARCHAR(500) NOT NULL,
        ArtigosUsadosJson    NVARCHAR(MAX) NULL,
        Score                DECIMAL(9,6) NULL,
        RespostaResumo       NVARCHAR(1200) NULL,
        Util                 BIT NULL,
        CriadoEm             DATETIME2(0) NOT NULL CONSTRAINT DF_HelpAIAnswerLog_CriadoEm DEFAULT(SYSUTCDATETIME())
    );

    CREATE INDEX IX_HelpAIAnswerLog_CriadoEm ON dbo.HelpAIAnswerLog (CriadoEm DESC);
END
GO
