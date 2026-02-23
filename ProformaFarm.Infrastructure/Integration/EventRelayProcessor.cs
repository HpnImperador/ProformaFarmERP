using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Application.Interfaces.Integration;

namespace ProformaFarm.Infrastructure.Integration;

public sealed class EventRelayProcessor : IEventRelayProcessor
{
    private static readonly Meter Meter = new("ProformaFarm.IntegrationRelay", "1.0.0");
    private static readonly Counter<int> DeliveredCounter = Meter.CreateCounter<int>("integration_relay_delivered_total");
    private static readonly Counter<int> FailedCounter = Meter.CreateCounter<int>("integration_relay_failed_total");
    private static readonly Counter<int> RetriedCounter = Meter.CreateCounter<int>("integration_relay_retried_total");

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IIntegrationEventTransport _transport;
    private readonly IntegrationRelayOptions _options;
    private readonly ILogger<EventRelayProcessor> _logger;

    public EventRelayProcessor(
        ISqlConnectionFactory connectionFactory,
        IIntegrationEventTransport transport,
        IOptions<IntegrationRelayOptions> options,
        ILogger<EventRelayProcessor> logger)
    {
        _connectionFactory = connectionFactory;
        _transport = transport;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        await SeedPendingDeliveriesAsync(connection, cancellationToken);

        var rows = (await connection.QueryAsync<DeliveryRow>(new CommandDefinition(
            @";WITH cte AS (
                SELECT TOP (@BatchSize) *
                FROM Integration.IntegrationDeliveryLog WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE (Status = @PendingStatus OR Status = @FailedStatus)
                  AND NextAttemptUtc <= SYSUTCDATETIME()
                  AND (LockedUntilUtc IS NULL OR LockedUntilUtc < SYSUTCDATETIME())
                ORDER BY CriadoEmUtc
            )
            UPDATE cte
            SET Status = @ProcessingStatus,
                LockedUntilUtc = DATEADD(SECOND, @LockSeconds, SYSUTCDATETIME())
            OUTPUT INSERTED.IdIntegrationDeliveryLog, INSERTED.OutboxEventId, INSERTED.IdIntegrationClient, INSERTED.OrganizacaoId, INSERTED.AttemptCount;",
            new
            {
                BatchSize = _options.BatchSize,
                PendingStatus = DeliveryStatus.Pending,
                FailedStatus = DeliveryStatus.Failed,
                ProcessingStatus = DeliveryStatus.Processing,
                LockSeconds = _options.LockSeconds
            },
            cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
            return 0;

        var delivered = 0;
        foreach (var row in rows)
        {
            if (await ProcessSingleAsync(connection, row, cancellationToken))
                delivered++;
        }

        return delivered;
    }

    private async Task SeedPendingDeliveriesAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Integration.IntegrationDeliveryLog
                (OutboxEventId, IdIntegrationClient, OrganizacaoId, EventType, CorrelationId, PayloadHash, Status, AttemptCount, NextAttemptUtc)
              SELECT
                e.Id,
                c.IdIntegrationClient,
                e.OrganizacaoId,
                e.EventType,
                e.CorrelationId,
                CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', CAST(e.Payload AS NVARCHAR(MAX))), 2),
                @PendingStatus,
                0,
                DATEADD(SECOND, -1, SYSUTCDATETIME())
              FROM Core.OutboxEvent e
              INNER JOIN Integration.IntegrationClient c
                ON c.OrganizacaoId = e.OrganizacaoId
               AND c.Ativo = 1
              WHERE e.Status = @OutboxProcessedStatus
                AND NOT EXISTS (
                    SELECT 1
                    FROM Integration.IntegrationDeliveryLog l
                    WHERE l.OutboxEventId = e.Id
                      AND l.IdIntegrationClient = c.IdIntegrationClient
                );",
            new
            {
                PendingStatus = DeliveryStatus.Pending,
                OutboxProcessedStatus = 2
            },
            cancellationToken: cancellationToken));
    }

    private async Task<bool> ProcessSingleAsync(IDbConnection connection, DeliveryRow row, CancellationToken cancellationToken)
    {
        var data = await connection.QueryFirstOrDefaultAsync<DeliveryDataRow>(new CommandDefinition(
            @"SELECT TOP (1)
                l.IdIntegrationDeliveryLog,
                l.AttemptCount,
                l.OutboxEventId,
                l.OrganizacaoId,
                l.IdIntegrationClient,
                l.CorrelationId,
                l.EventType,
                e.Payload,
                c.WebhookUrl,
                c.SecretKey
              FROM Integration.IntegrationDeliveryLog l
              INNER JOIN Core.OutboxEvent e ON e.Id = l.OutboxEventId
              INNER JOIN Integration.IntegrationClient c
                ON c.IdIntegrationClient = l.IdIntegrationClient
               AND c.OrganizacaoId = l.OrganizacaoId
              WHERE l.IdIntegrationDeliveryLog = @Id;",
            new { Id = row.IdIntegrationDeliveryLog },
            cancellationToken: cancellationToken));

        if (data is null)
        {
            await MarkAsTerminalFailureAsync(connection, row.IdIntegrationDeliveryLog, "Dados da entrega nao encontrados.", cancellationToken);
            FailedCounter.Add(1);
            return false;
        }

        var signature = ComputeSignature(data.SecretKey, data.Payload);
        var result = await _transport.SendAsync(new IntegrationTransportRequest
        {
            Url = data.WebhookUrl,
            EventType = data.EventType,
            Payload = data.Payload,
            CorrelationId = data.CorrelationId?.ToString(),
            SignatureHeaderName = _options.SignatureHeaderName,
            SignatureValue = signature
        }, cancellationToken);

        if (result.Success)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE Integration.IntegrationDeliveryLog
                  SET Status = @SentStatus,
                      AttemptCount = AttemptCount + 1,
                      LastAttemptUtc = SYSUTCDATETIME(),
                      LockedUntilUtc = NULL,
                      LastError = NULL,
                      ResponseStatusCode = @ResponseStatusCode,
                      ResponseBody = @ResponseBody
                  WHERE IdIntegrationDeliveryLog = @Id;",
                new
                {
                    Id = row.IdIntegrationDeliveryLog,
                    SentStatus = DeliveryStatus.Sent,
                    ResponseStatusCode = result.StatusCode,
                    ResponseBody = Truncate(result.ResponseBody, 1900)
                },
                cancellationToken: cancellationToken));

            _logger.LogInformation(
                "EventRelay entregue OutboxEventId={OutboxEventId} DeliveryId={DeliveryId} CorrelationId={CorrelationId}",
                row.OutboxEventId,
                row.IdIntegrationDeliveryLog,
                data.CorrelationId);
            DeliveredCounter.Add(1);
            return true;
        }

        await MarkAsRetryableFailureAsync(connection, row, result, cancellationToken);
        FailedCounter.Add(1);
        return false;
    }

    private async Task MarkAsRetryableFailureAsync(
        IDbConnection connection,
        DeliveryRow row,
        IntegrationTransportResult result,
        CancellationToken cancellationToken)
    {
        var nextAttemptCount = row.AttemptCount + 1;
        var terminal = nextAttemptCount >= _options.MaxRetries;
        var status = terminal ? DeliveryStatus.Failed : DeliveryStatus.Pending;
        var seconds = Math.Min(300, _options.RetryBaseDelaySeconds * (int)Math.Pow(2, Math.Min(6, nextAttemptCount)));
        var nextAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);
        var error = result.ErrorMessage ?? $"HTTP {result.StatusCode}";

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Integration.IntegrationDeliveryLog
              SET Status = @Status,
                  AttemptCount = @AttemptCount,
                  LastAttemptUtc = SYSUTCDATETIME(),
                  NextAttemptUtc = @NextAttemptUtc,
                  LockedUntilUtc = NULL,
                  LastError = @LastError,
                  ResponseStatusCode = @ResponseStatusCode,
                  ResponseBody = @ResponseBody
              WHERE IdIntegrationDeliveryLog = @Id;",
            new
            {
                Id = row.IdIntegrationDeliveryLog,
                Status = status,
                AttemptCount = nextAttemptCount,
                NextAttemptUtc = nextAttemptUtc,
                LastError = Truncate(error, 1900),
                ResponseStatusCode = result.StatusCode,
                ResponseBody = Truncate(result.ResponseBody, 1900)
            },
            cancellationToken: cancellationToken));

        RetriedCounter.Add(1);
        _logger.LogWarning(
            "EventRelay falhou OutboxEventId={OutboxEventId} DeliveryId={DeliveryId} Retry={RetryCount} Status={Status} CorrelationId={CorrelationId} Erro={Error}",
            row.OutboxEventId,
            row.IdIntegrationDeliveryLog,
            nextAttemptCount,
            status,
            row.CorrelationId,
            error);
    }

    private async Task MarkAsTerminalFailureAsync(
        IDbConnection connection,
        long deliveryId,
        string error,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Integration.IntegrationDeliveryLog
              SET Status = @FailedStatus,
                  LastAttemptUtc = SYSUTCDATETIME(),
                  LockedUntilUtc = NULL,
                  LastError = @LastError
              WHERE IdIntegrationDeliveryLog = @Id;",
            new
            {
                Id = deliveryId,
                FailedStatus = DeliveryStatus.Failed,
                LastError = Truncate(error, 1900)
            },
            cancellationToken: cancellationToken));
    }

    private static string? ComputeSignature(string? secretKey, string payload)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            return null;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    private sealed class DeliveryRow
    {
        public long IdIntegrationDeliveryLog { get; set; }
        public Guid OutboxEventId { get; set; }
        public int OrganizacaoId { get; set; }
        public int IdIntegrationClient { get; set; }
        public int AttemptCount { get; set; }
        public Guid? CorrelationId { get; set; }
    }

    private sealed class DeliveryDataRow
    {
        public long IdIntegrationDeliveryLog { get; set; }
        public int AttemptCount { get; set; }
        public Guid OutboxEventId { get; set; }
        public int OrganizacaoId { get; set; }
        public int IdIntegrationClient { get; set; }
        public Guid? CorrelationId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;
        public string? SecretKey { get; set; }
    }

    private static class DeliveryStatus
    {
        public const byte Pending = 0;
        public const byte Processing = 1;
        public const byte Sent = 2;
        public const byte Failed = 3;
    }
}
