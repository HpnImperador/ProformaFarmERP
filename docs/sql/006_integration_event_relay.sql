/* Integration Event Relay - Script idempotente
   Objetivo: infraestrutura de entrega Outbox -> Webhook
*/

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Integration')
BEGIN
    EXEC('CREATE SCHEMA Integration;');
END
GO

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
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Integration.IntegrationClient')
      AND name = N'IX_IntegrationClient_Organizacao_Ativo'
)
BEGIN
    CREATE INDEX IX_IntegrationClient_Organizacao_Ativo
        ON Integration.IntegrationClient (OrganizacaoId, Ativo);
END
GO

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
        Status TINYINT NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_Status DEFAULT (0), /* 0=Pending,1=Processing,2=Sent,3=Failed */
        AttemptCount INT NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_AttemptCount DEFAULT (0),
        NextAttemptUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_NextAttemptUtc DEFAULT (SYSUTCDATETIME()),
        LastAttemptUtc DATETIMEOFFSET(0) NULL,
        LockedUntilUtc DATETIMEOFFSET(0) NULL,
        LastError NVARCHAR(2000) NULL,
        ResponseStatusCode INT NULL,
        ResponseBody NVARCHAR(2000) NULL,
        CriadoEmUtc DATETIMEOFFSET(0) NOT NULL CONSTRAINT DF_Integration_IntegrationDeliveryLog_CriadoEmUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_IntegrationDeliveryLog_IntegrationClient FOREIGN KEY (IdIntegrationClient) REFERENCES Integration.IntegrationClient (IdIntegrationClient)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Integration.IntegrationDeliveryLog')
      AND name = N'UX_IntegrationDeliveryLog_Outbox_Client'
)
BEGIN
    CREATE UNIQUE INDEX UX_IntegrationDeliveryLog_Outbox_Client
        ON Integration.IntegrationDeliveryLog (OutboxEventId, IdIntegrationClient);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Integration.IntegrationDeliveryLog')
      AND name = N'IX_IntegrationDeliveryLog_Status_NextAttempt'
)
BEGIN
    CREATE INDEX IX_IntegrationDeliveryLog_Status_NextAttempt
        ON Integration.IntegrationDeliveryLog (Status, NextAttemptUtc, CriadoEmUtc);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'Integration.IntegrationDeliveryLog')
      AND name = N'IX_IntegrationDeliveryLog_Organizacao'
)
BEGIN
    CREATE INDEX IX_IntegrationDeliveryLog_Organizacao
        ON Integration.IntegrationDeliveryLog (OrganizacaoId, CriadoEmUtc DESC);
END
GO
