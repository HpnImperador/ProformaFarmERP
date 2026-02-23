using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ProformaFarm.Application.Tests.Common;

public static class OutboxTestDataSetup
{
    public static async Task<OutboxTestDataResult> EnsureAsync(CustomWebApplicationFactory factory)
    {
        var org = await OrganizacaoTestDataSetup.EnsureAsync(factory);

        _ = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada para testes.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(
            "EXEC sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;",
            new { r = "PF_IT_OUTBOX_SETUP" },
            tx);

        await EnsureOutboxSchemaAsync(connection, tx);
        await tx.CommitAsync();

        return new OutboxTestDataResult
        {
            IdOrganizacao = org.IdOrganizacao,
            IdUnidade = org.IdUnidade,
            Login = org.Login,
            Senha = org.Senha
        };
    }

    private static Task EnsureOutboxSchemaAsync(IDbConnection connection, IDbTransaction transaction)
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
