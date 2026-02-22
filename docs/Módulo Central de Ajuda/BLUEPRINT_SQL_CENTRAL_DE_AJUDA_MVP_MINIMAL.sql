/* BLUEPRINT SQL (MVP MINIMALISTA) — CENTRAL DE AJUDA (HELP CENTER)
   Banco: SQL Server
   Objetivo: reduzir atrito na primeira entrega. Inclui apenas:
   - Categorias
   - Artigos (metadados)
   - Versões de artigo (conteúdo)
   - Logs mínimos (busca, visualização, feedback)
   Observação: recursos avançados (tags, FAQ, contexto, IA, fulltext) ficam em scripts opcionais.
*/

/* 1) Categorias */
IF OBJECT_ID('dbo.HelpCategoria', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpCategoria
    (
        IdHelpCategoria       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpCategoria PRIMARY KEY,
        Nome                 NVARCHAR(120) NOT NULL,
        Slug                 NVARCHAR(140) NOT NULL,
        Ordem                INT NOT NULL CONSTRAINT DF_HelpCategoria_Ordem DEFAULT(0),
        Ativa                BIT NOT NULL CONSTRAINT DF_HelpCategoria_Ativa DEFAULT(1),
        DataCriacao          DATETIME2(0) NOT NULL CONSTRAINT DF_HelpCategoria_DataCriacao DEFAULT(SYSUTCDATETIME()),
        DataAtualizacao      DATETIME2(0) NULL
    );

    CREATE UNIQUE INDEX UX_HelpCategoria_Slug ON dbo.HelpCategoria (Slug);
END
GO

/* 2) Artigos (metadados principais) */
IF OBJECT_ID('dbo.HelpArtigo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpArtigo
    (
        IdHelpArtigo            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpArtigo PRIMARY KEY,
        IdHelpCategoria         INT NOT NULL,
        Titulo                  NVARCHAR(200) NOT NULL,
        Slug                    NVARCHAR(220) NOT NULL,
        Resumo                  NVARCHAR(500) NULL,
        Status                  TINYINT NOT NULL, /* 0=Rascunho,1=Publicado,2=Arquivado */
        VersaoAtual             INT NOT NULL CONSTRAINT DF_HelpArtigo_VersaoAtual DEFAULT(1),
        PublicadoEm             DATETIME2(0) NULL,
        AtualizadoEm            DATETIME2(0) NULL,
        CriadoPorUsuarioId      INT NOT NULL,
        AtualizadoPorUsuarioId  INT NULL
    );

    ALTER TABLE dbo.HelpArtigo
        ADD CONSTRAINT FK_HelpArtigo_HelpCategoria
        FOREIGN KEY (IdHelpCategoria) REFERENCES dbo.HelpCategoria(IdHelpCategoria)
        ON DELETE NO ACTION;

    CREATE UNIQUE INDEX UX_HelpArtigo_Slug ON dbo.HelpArtigo (Slug);
    CREATE INDEX IX_HelpArtigo_Categoria_Status ON dbo.HelpArtigo (IdHelpCategoria, Status);
END
GO

/* 3) Versões do artigo (conteúdo versionado) */
IF OBJECT_ID('dbo.HelpArtigoVersao', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpArtigoVersao
    (
        IdHelpArtigoVersao   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpArtigoVersao PRIMARY KEY,
        IdHelpArtigo         INT NOT NULL,
        Versao               INT NOT NULL,
        ConteudoMarkdown     NVARCHAR(MAX) NOT NULL,
        ConteudoHtml         NVARCHAR(MAX) NULL,
        Publicado            BIT NOT NULL CONSTRAINT DF_HelpArtigoVersao_Publicado DEFAULT(0),
        PublicadoEm          DATETIME2(0) NULL,
        CriadoEm             DATETIME2(0) NOT NULL CONSTRAINT DF_HelpArtigoVersao_CriadoEm DEFAULT(SYSUTCDATETIME()),
        CriadoPorUsuarioId   INT NOT NULL
    );

    ALTER TABLE dbo.HelpArtigoVersao
        ADD CONSTRAINT FK_HelpArtigoVersao_HelpArtigo
        FOREIGN KEY (IdHelpArtigo) REFERENCES dbo.HelpArtigo(IdHelpArtigo)
        ON DELETE NO ACTION;

    CREATE UNIQUE INDEX UX_HelpArtigoVersao_Artigo_Versao ON dbo.HelpArtigoVersao (IdHelpArtigo, Versao);
    CREATE INDEX IX_HelpArtigoVersao_Artigo_Publicado ON dbo.HelpArtigoVersao (IdHelpArtigo, Publicado);
END
GO

/* 4) Log mínimo: busca */
IF OBJECT_ID('dbo.HelpSearchLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpSearchLog
    (
        IdHelpSearchLog      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpSearchLog PRIMARY KEY,
        UsuarioId            INT NULL,
        Termo                NVARCHAR(300) NOT NULL,
        FiltrosJson          NVARCHAR(2000) NULL,
        ResultadoCount       INT NOT NULL,
        SemResultado         BIT NOT NULL,
        CriadoEm             DATETIME2(0) NOT NULL CONSTRAINT DF_HelpSearchLog_CriadoEm DEFAULT(SYSUTCDATETIME())
    );

    CREATE INDEX IX_HelpSearchLog_CriadoEm ON dbo.HelpSearchLog (CriadoEm DESC);
    CREATE INDEX IX_HelpSearchLog_SemResultado ON dbo.HelpSearchLog (SemResultado, CriadoEm DESC);
END
GO

/* 5) Log mínimo: visualização de artigo */
IF OBJECT_ID('dbo.HelpArticleView', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpArticleView
    (
        IdHelpArticleView    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpArticleView PRIMARY KEY,
        UsuarioId            INT NULL,
        IdHelpArtigo         INT NOT NULL,
        IdHelpArtigoVersao   INT NULL,
        OrigemModulo         NVARCHAR(80) NULL,
        OrigemTela           NVARCHAR(120) NULL,
        CriadoEm             DATETIME2(0) NOT NULL CONSTRAINT DF_HelpArticleView_CriadoEm DEFAULT(SYSUTCDATETIME())
    );

    ALTER TABLE dbo.HelpArticleView
        ADD CONSTRAINT FK_HelpArticleView_Artigo
        FOREIGN KEY (IdHelpArtigo) REFERENCES dbo.HelpArtigo(IdHelpArtigo)
        ON DELETE NO ACTION;

    CREATE INDEX IX_HelpArticleView_Artigo_CriadoEm ON dbo.HelpArticleView (IdHelpArtigo, CriadoEm DESC);
END
GO

/* 6) Log mínimo: feedback */
IF OBJECT_ID('dbo.HelpFeedback', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.HelpFeedback
    (
        IdHelpFeedback       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HelpFeedback PRIMARY KEY,
        UsuarioId            INT NULL,
        IdHelpArtigo         INT NOT NULL,
        Util                 BIT NOT NULL,
        Comentario           NVARCHAR(800) NULL,
        CriadoEm             DATETIME2(0) NOT NULL CONSTRAINT DF_HelpFeedback_CriadoEm DEFAULT(SYSUTCDATETIME())
    );

    ALTER TABLE dbo.HelpFeedback
        ADD CONSTRAINT FK_HelpFeedback_Artigo
        FOREIGN KEY (IdHelpArtigo) REFERENCES dbo.HelpArtigo(IdHelpArtigo)
        ON DELETE NO ACTION;

    CREATE INDEX IX_HelpFeedback_Artigo_CriadoEm ON dbo.HelpFeedback (IdHelpArtigo, CriadoEm DESC);
END
GO

/* NOTA (MVP):
   - Busca inicial pode ser LIKE/CONTAINS (quando FULLTEXT habilitado).
   - No MVP, a API deve exigir paginação e impor limite máximo.
*/
