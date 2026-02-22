-- BLUEPRINT SQL – INTEGRACOES N8N
-- ProformaFarm ERP

CREATE SCHEMA Integracao;

CREATE TABLE Integracao.IntegrationClient (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    Nome NVARCHAR(200) NOT NULL,
    ApiKeyHash NVARCHAR(256) NOT NULL,
    PermissoesJson NVARCHAR(MAX) NULL,
    Ativo BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIMEOFFSET NOT NULL
);

CREATE INDEX IX_IntegrationClient_Org
ON Integracao.IntegrationClient (OrganizacaoId);

CREATE TABLE Integracao.IntegrationDeliveryLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OutboxEventId UNIQUEIDENTIFIER NOT NULL,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    Destino NVARCHAR(200) NOT NULL,
    HttpStatus INT NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    Status INT NOT NULL DEFAULT 0,
    CorrelationId UNIQUEIDENTIFIER NULL,
    LastAttemptAt DATETIMEOFFSET NULL,
    CreatedAt DATETIMEOFFSET NOT NULL
);

CREATE INDEX IX_IntegrationDelivery_Status
ON Integracao.IntegrationDeliveryLog (Status, LastAttemptAt);

CREATE TABLE Integracao.IdempotencyLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    IdempotencyKey NVARCHAR(200) NOT NULL,
    Endpoint NVARCHAR(200) NOT NULL,
    RequestHash NVARCHAR(256) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL
);

CREATE UNIQUE INDEX UX_Idempotency_Key
ON Integracao.IdempotencyLog (OrganizacaoId, IdempotencyKey);
