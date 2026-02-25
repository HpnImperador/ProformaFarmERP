using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Application.Services.Security;

namespace ProformaFarm.Application.Tests.Common;

public static class OutboxTestDataSetup
{
    private const string Login = "it_org_admin";
    private const string Senha = "It@Org123";
    private const string Nome = "IT Organizacao Admin";
    private const string Cnpj = "99999999000199";
    private const string CodigoMatriz = "IT-MATRIZ";
    private const string CodigoCargo = "IT-CARGO-GER";

    public static async Task<OutboxTestDataResult> EnsureAsync(CustomWebApplicationFactory factory)
    {
        _ = factory.CreateClient();

        var sqlFactory = factory.Services.GetRequiredService<ISqlConnectionFactory>();
        var isPostgres = sqlFactory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || sqlFactory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);

        if (!isPostgres)
        {
            var org = await OrganizacaoTestDataSetup.EnsureAsync(factory);
            await EnsureSqlServerOutboxSchemaAsync(factory);

            return new OutboxTestDataResult
            {
                IdOrganizacao = org.IdOrganizacao,
                IdUnidade = org.IdUnidade,
                Login = org.Login,
                Senha = org.Senha
            };
        }

            return await EnsurePostgresOutboxDataAsync(factory, sqlFactory);
    }

    private static async Task EnsureSqlServerOutboxSchemaAsync(CustomWebApplicationFactory factory)
    {
        var configuration = factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada para testes.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(
            "EXEC sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;",
            new { r = "PF_IT_OUTBOX_SETUP" },
            tx);

        await EnsureOutboxSchemaSqlServerAsync(connection, tx);
        await tx.CommitAsync();
    }

    private static async Task<OutboxTestDataResult> EnsurePostgresOutboxDataAsync(CustomWebApplicationFactory factory, ISqlConnectionFactory sqlFactory)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var pgConnection = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection")
            ?? configuration.GetConnectionString("PostgresConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string PostgreSQL nao configurada para testes.");

        using var db = sqlFactory.CreateConnection();
        if (db is not DbConnection cn)
            throw new InvalidOperationException("Conexao de banco nao suportada para testes.");
        cn.ConnectionString = pgConnection;

        await cn.OpenAsync();
        await using var tx = await cn.BeginTransactionAsync();

        await cn.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtext(@r));", new { r = "PF_IT_OUTBOX_SETUP" }, tx);

        var passwordService = new PasswordService();
        var (hash, salt) = passwordService.HashPassword(Senha);

        var idUsuario = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdUsuario"" FROM public.""Usuario"" WHERE ""Login"" = @Login LIMIT 1;",
            new { Login },
            tx);

        if (idUsuario.HasValue)
        {
            await cn.ExecuteAsync(
                @"UPDATE public.""Usuario""
                  SET ""Nome"" = @Nome,
                      ""SenhaHash"" = @Hash,
                      ""SenhaSalt"" = @Salt,
                      ""Ativo"" = TRUE
                  WHERE ""IdUsuario"" = @IdUsuario;",
                new { Nome, Hash = hash, Salt = salt, IdUsuario = idUsuario.Value },
                tx);
        }
        else
        {
            idUsuario = await cn.ExecuteScalarAsync<int>(
                @"INSERT INTO public.""Usuario"" (""Nome"", ""Login"", ""SenhaHash"", ""SenhaSalt"", ""Ativo"", ""DataCriacao"")
                  VALUES (@Nome, @Login, @Hash, @Salt, TRUE, TIMEZONE('UTC', NOW()))
                  RETURNING ""IdUsuario"";",
                new { Nome, Login, Hash = hash, Salt = salt },
                tx);
        }

        var idOrganizacao = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdOrganizacao"" FROM public.""Organizacao"" WHERE ""Cnpj"" = @Cnpj LIMIT 1;",
            new { Cnpj },
            tx);

        if (!idOrganizacao.HasValue)
        {
            idOrganizacao = await cn.ExecuteScalarAsync<int>(
                @"INSERT INTO public.""Organizacao"" (""RazaoSocial"", ""NomeFantasia"", ""Cnpj"", ""Ativa"", ""DataCriacao"")
                  VALUES ('IT Organizacao Ltda', 'IT Org', @Cnpj, TRUE, TIMEZONE('UTC', NOW()))
                  RETURNING ""IdOrganizacao"";",
                new { Cnpj },
                tx);
        }

        var idMatriz = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdUnidadeOrganizacional""
              FROM public.""UnidadeOrganizacional""
              WHERE ""IdOrganizacao"" = @IdOrganizacao AND ""Codigo"" = @Codigo
              LIMIT 1;",
            new { IdOrganizacao = idOrganizacao.Value, Codigo = CodigoMatriz },
            tx);

        if (!idMatriz.HasValue)
        {
            idMatriz = await cn.ExecuteScalarAsync<int>(
                @"INSERT INTO public.""UnidadeOrganizacional""
                  (""IdOrganizacao"", ""IdUnidadePai"", ""Tipo"", ""Codigo"", ""Nome"", ""Ativa"", ""DataInicio"", ""DataFim"")
                  VALUES (@IdOrganizacao, NULL, 'Matriz', @Codigo, 'IT Matriz', TRUE, TIMEZONE('UTC', NOW()), NULL)
                  RETURNING ""IdUnidadeOrganizacional"";",
                new { IdOrganizacao = idOrganizacao.Value, Codigo = CodigoMatriz },
                tx);
        }

        var idCargo = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdCargo"" FROM public.""Cargo""
              WHERE ""IdOrganizacao"" = @IdOrganizacao AND ""Codigo"" = @Codigo
              LIMIT 1;",
            new { IdOrganizacao = idOrganizacao.Value, Codigo = CodigoCargo },
            tx);

        if (!idCargo.HasValue)
        {
            idCargo = await cn.ExecuteScalarAsync<int>(
                @"INSERT INTO public.""Cargo"" (""IdOrganizacao"", ""Codigo"", ""Nome"", ""Ativo"")
                  VALUES (@IdOrganizacao, @Codigo, 'IT Gerente', TRUE)
                  RETURNING ""IdCargo"";",
                new { IdOrganizacao = idOrganizacao.Value, Codigo = CodigoCargo },
                tx);
        }

        var lotacao = await cn.ExecuteScalarAsync<int?>(
            @"SELECT ""IdLotacaoUsuario""
              FROM public.""LotacaoUsuario""
              WHERE ""IdUsuario"" = @IdUsuario AND ""Principal"" = TRUE AND ""Ativa"" = TRUE
              LIMIT 1;",
            new { IdUsuario = idUsuario.Value },
            tx);

        if (!lotacao.HasValue)
        {
            await cn.ExecuteAsync(
                @"INSERT INTO public.""LotacaoUsuario""
                  (""IdUsuario"", ""IdUnidadeOrganizacional"", ""IdCargo"", ""DataInicio"", ""DataFim"", ""Principal"", ""Ativa"")
                  VALUES (@IdUsuario, @IdUnidade, @IdCargo, TIMEZONE('UTC', NOW()), NULL, TRUE, TRUE);",
                new { IdUsuario = idUsuario.Value, IdUnidade = idMatriz.Value, IdCargo = idCargo.Value },
                tx);
        }

        await tx.CommitAsync();

        return new OutboxTestDataResult
        {
            IdOrganizacao = idOrganizacao.Value,
            IdUnidade = idMatriz.Value,
            Login = Login,
            Senha = Senha
        };
    }

    private static Task EnsureOutboxSchemaSqlServerAsync(IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core')
BEGIN
    EXEC(N'CREATE SCHEMA Core');
END;

IF OBJECT_ID(N'Core.OutboxEvent', N'U') IS NULL
BEGIN
    CREATE TABLE Core.OutboxEvent
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Core_OutboxEvent PRIMARY KEY,
        OrganizacaoId INT NOT NULL,
        EventType NVARCHAR(300) NOT NULL,
        Payload NVARCHAR(MAX) NOT NULL,
        OccurredOnUtc DATETIMEOFFSET(0) NOT NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_Core_OutboxEvent_Status DEFAULT (0),
        RetryCount INT NOT NULL CONSTRAINT DF_Core_OutboxEvent_RetryCount DEFAULT (0),
        NextAttemptUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Core_OutboxEvent_NextAttemptUtc DEFAULT (SYSUTCDATETIME()),
        ProcessedOnUtc DATETIMEOFFSET(0) NULL,
        LockedUntilUtc DATETIMEOFFSET(0) NULL,
        LastError NVARCHAR(2000) NULL
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Core.OutboxEvent')
      AND name = N'IX_Core_OutboxEvent_Status_NextAttemptUtc'
)
BEGIN
    CREATE INDEX IX_Core_OutboxEvent_Status_NextAttemptUtc
        ON Core.OutboxEvent (Status, NextAttemptUtc, OccurredOnUtc);
END;

IF OBJECT_ID(N'Core.OutboxProcessedEvent', N'U') IS NULL
BEGIN
    CREATE TABLE Core.OutboxProcessedEvent
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Core_OutboxProcessedEvent PRIMARY KEY,
        EventId UNIQUEIDENTIFIER NOT NULL,
        HandlerName NVARCHAR(200) NOT NULL,
        ProcessedOnUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Core_OutboxProcessedEvent_ProcessedOnUtc DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Core.OutboxProcessedEvent')
      AND name = N'UX_Core_OutboxProcessedEvent_Event_Handler'
)
BEGIN
    CREATE UNIQUE INDEX UX_Core_OutboxProcessedEvent_Event_Handler
        ON Core.OutboxProcessedEvent (EventId, HandlerName);
END;

IF OBJECT_ID(N'Core.OutboxHelloProbe', N'U') IS NULL
BEGIN
    CREATE TABLE Core.OutboxHelloProbe
    (
        IdOutboxHelloProbe UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Core_OutboxHelloProbe PRIMARY KEY,
        OrganizacaoId INT NOT NULL,
        NomeEvento NVARCHAR(120) NOT NULL,
        SimularFalhaUmaVez BIT NOT NULL CONSTRAINT DF_Core_OutboxHelloProbe_SimularFalhaUmaVez DEFAULT (0),
        ProcessedCount INT NOT NULL CONSTRAINT DF_Core_OutboxHelloProbe_ProcessedCount DEFAULT (0),
        CriadoEmUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Core_OutboxHelloProbe_CriadoEmUtc DEFAULT (SYSUTCDATETIME()),
        UltimoProcessamentoUtc DATETIMEOFFSET(0) NULL
    );
END;

IF OBJECT_ID(N'Core.EstoqueBaixoNotificacao', N'U') IS NULL
BEGIN
    CREATE TABLE Core.EstoqueBaixoNotificacao
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Core_EstoqueBaixoNotificacao PRIMARY KEY,
        EventId UNIQUEIDENTIFIER NOT NULL,
        OrganizacaoId INT NOT NULL,
        IdUnidadeOrganizacional INT NOT NULL,
        IdProduto INT NOT NULL,
        IdLote INT NULL,
        QuantidadeDisponivel DECIMAL(18,4) NOT NULL,
        QuantidadeReservada DECIMAL(18,4) NOT NULL,
        QuantidadeLiquida DECIMAL(18,4) NOT NULL,
        LimiteEstoqueBaixo DECIMAL(18,4) NOT NULL,
        OrigemMovimento NVARCHAR(40) NOT NULL,
        DocumentoReferencia NVARCHAR(120) NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        DetectadoEmUtc DATETIMEOFFSET(0) NOT NULL,
        RegistradoEmUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Core_EstoqueBaixoNotificacao_RegistradoEmUtc DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Core.EstoqueBaixoNotificacao')
      AND name = N'UX_Core_EstoqueBaixoNotificacao_EventId'
)
BEGIN
    CREATE UNIQUE INDEX UX_Core_EstoqueBaixoNotificacao_EventId
        ON Core.EstoqueBaixoNotificacao (EventId);
END;

IF OBJECT_ID(N'Core.EstoqueRepostoNotificacao', N'U') IS NULL
BEGIN
    CREATE TABLE Core.EstoqueRepostoNotificacao
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Core_EstoqueRepostoNotificacao PRIMARY KEY,
        EventId UNIQUEIDENTIFIER NOT NULL,
        OrganizacaoId INT NOT NULL,
        IdUnidadeOrganizacional INT NOT NULL,
        IdProduto INT NOT NULL,
        IdLote INT NULL,
        QuantidadeLiquidaAntes DECIMAL(18,4) NOT NULL,
        QuantidadeLiquidaDepois DECIMAL(18,4) NOT NULL,
        LimiteEstoqueBaixo DECIMAL(18,4) NOT NULL,
        OrigemMovimento NVARCHAR(40) NOT NULL,
        DocumentoReferencia NVARCHAR(120) NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        DetectadoEmUtc DATETIMEOFFSET(0) NOT NULL,
        RegistradoEmUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Core_EstoqueRepostoNotificacao_RegistradoEmUtc DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Core.EstoqueRepostoNotificacao')
      AND name = N'UX_Core_EstoqueRepostoNotificacao_EventId'
)
BEGIN
    CREATE UNIQUE INDEX UX_Core_EstoqueRepostoNotificacao_EventId
        ON Core.EstoqueRepostoNotificacao (EventId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Core.EstoqueRepostoNotificacao')
      AND name = N'IX_Core_EstoqueRepostoNotificacao_Org_Produto'
)
BEGIN
    CREATE INDEX IX_Core_EstoqueRepostoNotificacao_Org_Produto
        ON Core.EstoqueRepostoNotificacao (OrganizacaoId, IdProduto, DetectadoEmUtc DESC);
END;";

        return connection.ExecuteAsync(sql, transaction: transaction);
    }
}

public sealed class OutboxTestDataResult
{
    public int IdOrganizacao { get; init; }
    public int IdUnidade { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
}
