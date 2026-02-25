using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProformaFarm.Application.Interfaces.Data;

namespace ProformaFarm.Application.Tests.Common;

public static class IntegrationRelayTestDataSetup
{
    public static async Task<IntegrationRelayTestDataResult> EnsureAsync(
        CustomWebApplicationFactory factory,
        int idOrganizacao,
        string webhookUrl)
    {
        _ = factory.CreateClient();
        var sqlFactory = factory.Services.GetRequiredService<ISqlConnectionFactory>();
        var isPostgres = sqlFactory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || sqlFactory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);

        return isPostgres
            ? await EnsurePostgresAsync(factory, sqlFactory, idOrganizacao, webhookUrl)
            : await EnsureSqlServerAsync(factory, idOrganizacao, webhookUrl);
    }

    private static async Task<IntegrationRelayTestDataResult> EnsureSqlServerAsync(
        CustomWebApplicationFactory factory,
        int idOrganizacao,
        string webhookUrl)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada para testes.");

        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();
        await using var tx = await cn.BeginTransactionAsync();

        await EnsureSchemaSqlServerAsync(cn, tx);

        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE Integration.IntegrationClient
              SET Ativo = 0,
                  AtualizadoEmUtc = SYSUTCDATETIME()
              WHERE OrganizacaoId = @OrganizacaoId
                AND Ativo = 1;",
            new { OrganizacaoId = idOrganizacao },
            tx));

        var nome = $"IT-RELAY-{Guid.NewGuid():N}"[..16];
        var idIntegrationClient = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Integration.IntegrationClient
                (OrganizacaoId, Nome, WebhookUrl, SecretKey, Ativo)
              OUTPUT INSERTED.IdIntegrationClient
              VALUES (@OrganizacaoId, @Nome, @WebhookUrl, @SecretKey, 1);",
            new
            {
                OrganizacaoId = idOrganizacao,
                Nome = nome,
                WebhookUrl = webhookUrl,
                SecretKey = "it-secret-key"
            },
            tx));

        await tx.CommitAsync();

        return new IntegrationRelayTestDataResult
        {
            IdIntegrationClient = idIntegrationClient,
            IdOrganizacao = idOrganizacao,
            WebhookUrl = webhookUrl
        };
    }

    private static async Task<IntegrationRelayTestDataResult> EnsurePostgresAsync(
        CustomWebApplicationFactory factory,
        ISqlConnectionFactory sqlFactory,
        int idOrganizacao,
        string webhookUrl)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgresConnection")
            ?? configuration.GetConnectionString("PostgresConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string PostgreSQL nao configurada para testes.");

        using var db = sqlFactory.CreateConnection();
        if (db is not DbConnection cn)
            throw new InvalidOperationException("Conexao de banco nao suportada para testes.");
        cn.ConnectionString = connectionString;

        await cn.OpenAsync();
        await using var tx = await cn.BeginTransactionAsync();

        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE ""Integration"".""IntegrationClient""
              SET ""Ativo"" = FALSE,
                  ""AtualizadoEmUtc"" = TIMEZONE('UTC', NOW())
              WHERE ""OrganizacaoId"" = @OrganizacaoId
                AND ""Ativo"" = TRUE;",
            new { OrganizacaoId = idOrganizacao },
            tx));

        var nome = $"IT-RELAY-{Guid.NewGuid():N}"[..16];
        var idIntegrationClient = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO ""Integration"".""IntegrationClient""
                (""OrganizacaoId"", ""Nome"", ""WebhookUrl"", ""SecretKey"", ""Ativo"")
              VALUES (@OrganizacaoId, @Nome, @WebhookUrl, @SecretKey, TRUE)
              RETURNING ""IdIntegrationClient"";",
            new
            {
                OrganizacaoId = idOrganizacao,
                Nome = nome,
                WebhookUrl = webhookUrl,
                SecretKey = "it-secret-key"
            },
            tx));

        await tx.CommitAsync();

        return new IntegrationRelayTestDataResult
        {
            IdIntegrationClient = idIntegrationClient,
            IdOrganizacao = idOrganizacao,
            WebhookUrl = webhookUrl
        };
    }

    private static Task EnsureSchemaSqlServerAsync(IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Integration')
BEGIN
    EXEC('CREATE SCHEMA Integration;');
END;

IF OBJECT_ID(N'Integration.IntegrationClient', N'U') IS NULL
BEGIN
    CREATE TABLE Integration.IntegrationClient
    (
        IdIntegrationClient INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Integration_IntegrationClient PRIMARY KEY,
        OrganizacaoId INT NOT NULL,
        Nome NVARCHAR(120) NOT NULL,
        WebhookUrl NVARCHAR(500) NOT NULL,
        SecretKey NVARCHAR(300) NULL,
        Ativo BIT NOT NULL CONSTRAINT DF_Integration_IntegrationClient_Ativo DEFAULT (1),
        CriadoEmUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Integration_IntegrationClient_CriadoEmUtc DEFAULT (SYSUTCDATETIME()),
        AtualizadoEmUtc DATETIMEOFFSET(0) NULL
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Integration.IntegrationClient')
      AND name = N'IX_IntegrationClient_Organizacao_Ativo'
)
BEGIN
    CREATE INDEX IX_IntegrationClient_Organizacao_Ativo
        ON Integration.IntegrationClient (OrganizacaoId, Ativo);
END;

IF OBJECT_ID(N'Integration.IntegrationDeliveryLog', N'U') IS NULL
BEGIN
    CREATE TABLE Integration.IntegrationDeliveryLog
    (
        IdIntegrationDeliveryLog BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Integration_IntegrationDeliveryLog PRIMARY KEY,
        OutboxEventId UNIQUEIDENTIFIER NOT NULL,
        IdIntegrationClient INT NOT NULL,
        OrganizacaoId INT NOT NULL,
        EventType NVARCHAR(300) NOT NULL,
        CorrelationId UNIQUEIDENTIFIER NULL,
        PayloadHash VARCHAR(64) NOT NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_Status DEFAULT (0),
        AttemptCount INT NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_AttemptCount DEFAULT (0),
        NextAttemptUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_NextAttemptUtc DEFAULT (SYSUTCDATETIME()),
        LastAttemptUtc DATETIMEOFFSET(0) NULL,
        LockedUntilUtc DATETIMEOFFSET(0) NULL,
        LastError NVARCHAR(2000) NULL,
        ResponseStatusCode INT NULL,
        ResponseBody NVARCHAR(2000) NULL,
        CriadoEmUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_CriadoEmUtc DEFAULT (SYSUTCDATETIME())
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Integration.IntegrationDeliveryLog')
      AND name = N'UX_IntegrationDeliveryLog_Outbox_Client'
)
BEGIN
    CREATE UNIQUE INDEX UX_IntegrationDeliveryLog_Outbox_Client
        ON Integration.IntegrationDeliveryLog (OutboxEventId, IdIntegrationClient);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Integration.IntegrationDeliveryLog')
      AND name = N'IX_IntegrationDeliveryLog_Status_NextAttempt'
)
BEGIN
    CREATE INDEX IX_IntegrationDeliveryLog_Status_NextAttempt
        ON Integration.IntegrationDeliveryLog (Status, NextAttemptUtc, CriadoEmUtc);
END;";

        return connection.ExecuteAsync(sql, transaction: transaction);
    }
}

public sealed class IntegrationRelayTestDataResult
{
    public int IdIntegrationClient { get; init; }
    public int IdOrganizacao { get; init; }
    public string WebhookUrl { get; init; } = string.Empty;
}
