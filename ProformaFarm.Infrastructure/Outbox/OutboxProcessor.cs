using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Application.Interfaces.Outbox;

namespace ProformaFarm.Infrastructure.Outbox;

public sealed class OutboxProcessor : IOutboxProcessor
{
    private static readonly Meter Meter = new("ProformaFarm.Outbox", "1.0.0");
    private static readonly Counter<int> ProcessedCounter = Meter.CreateCounter<int>("outbox_events_processed_total");
    private static readonly Counter<int> FailedCounter = Meter.CreateCounter<int>("outbox_events_failed_total");
    private static readonly Counter<int> RetriedCounter = Meter.CreateCounter<int>("outbox_events_retried_total");

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IReadOnlyDictionary<string, IOutboxEventHandler> _handlers;
    private readonly OutboxProcessingOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        ISqlConnectionFactory connectionFactory,
        IEnumerable<IOutboxEventHandler> handlers,
        IOptions<OutboxProcessingOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _connectionFactory = connectionFactory;
        _handlers = handlers.ToDictionary(x => x.EventType, StringComparer.Ordinal);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var events = (await connection.QueryAsync<OutboxEventRow>(new CommandDefinition(
            @";WITH cte AS (
                SELECT TOP (@BatchSize) *
                FROM Core.OutboxEvent WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE Status = @PendingStatus
                  AND NextAttemptUtc <= SYSUTCDATETIME()
                  AND (LockedUntilUtc IS NULL OR LockedUntilUtc < SYSUTCDATETIME())
                ORDER BY OccurredOnUtc
            )
            UPDATE cte
            SET Status = @ProcessingStatus,
                LockedUntilUtc = DATEADD(SECOND, @LockSeconds, SYSUTCDATETIME())
            OUTPUT INSERTED.Id, INSERTED.OrganizacaoId, INSERTED.EventType, INSERTED.Payload, INSERTED.CorrelationId, INSERTED.RetryCount;",
            new
            {
                BatchSize = _options.BatchSize,
                PendingStatus = OutboxEventStatus.Pending,
                ProcessingStatus = OutboxEventStatus.Processing,
                LockSeconds = _options.LockSeconds
            },
            cancellationToken: cancellationToken))).ToList();

        if (events.Count == 0)
            return 0;

        var totalProcessed = 0;
        foreach (var row in events)
        {
            var ok = await ProcessSingleAsync(connection, row, cancellationToken);
            if (ok)
                totalProcessed++;
        }

        return totalProcessed;
    }

    private async Task<bool> ProcessSingleAsync(IDbConnection connection, OutboxEventRow row, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(row.EventType, out var handler))
        {
            await MarkFailedAsync(connection, row, $"Handler nao encontrado para EventType={row.EventType}", cancellationToken);
            FailedCounter.Add(1);
            return false;
        }

        if (await AlreadyProcessedAsync(connection, row.Id, handler.HandlerName, cancellationToken))
        {
            await MarkProcessedAsync(connection, row.Id, cancellationToken);
            _logger.LogInformation("Outbox {EventId} ignorado por idempotencia (Handler={Handler}).", row.Id, handler.HandlerName);
            return true;
        }

        var payload = DeserializePayload(row, handler.PayloadType);
        var context = new OutboxProcessContext
        {
            EventId = row.Id,
            CorrelationId = row.CorrelationId,
            OrganizacaoId = row.OrganizacaoId,
            RetryCount = row.RetryCount
        };

        using var tx = connection.BeginTransaction();
        try
        {
            await handler.HandleAsync(payload, context, connection, tx, cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO Core.OutboxProcessedEvent (EventId, HandlerName, ProcessedOnUtc)
                  VALUES (@EventId, @HandlerName, SYSUTCDATETIME());",
                new { EventId = row.Id, HandlerName = handler.HandlerName },
                tx,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE Core.OutboxEvent
                  SET Status = @ProcessedStatus,
                      ProcessedOnUtc = SYSUTCDATETIME(),
                      LockedUntilUtc = NULL,
                      LastError = NULL
                  WHERE Id = @EventId;",
                new
                {
                    EventId = row.Id,
                    ProcessedStatus = OutboxEventStatus.Processed
                },
                tx,
                cancellationToken: cancellationToken));

            tx.Commit();

            _logger.LogInformation(
                "Outbox processado EventId={EventId} CorrelationId={CorrelationId} Handler={Handler}",
                row.Id,
                row.CorrelationId,
                handler.HandlerName);
            ProcessedCounter.Add(1);
            return true;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            await MarkFailedAsync(connection, row, ex.Message, cancellationToken);
            FailedCounter.Add(1);
            return false;
        }
    }

    private async Task<bool> AlreadyProcessedAsync(IDbConnection connection, Guid eventId, string handlerName, CancellationToken cancellationToken)
    {
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1)
              FROM Core.OutboxProcessedEvent
              WHERE EventId = @EventId AND HandlerName = @HandlerName;",
            new { EventId = eventId, HandlerName = handlerName },
            cancellationToken: cancellationToken));
        return total > 0;
    }

    private async Task MarkProcessedAsync(IDbConnection connection, Guid eventId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Core.OutboxEvent
              SET Status = @ProcessedStatus,
                  ProcessedOnUtc = SYSUTCDATETIME(),
                  LockedUntilUtc = NULL
              WHERE Id = @EventId;",
            new
            {
                EventId = eventId,
                ProcessedStatus = OutboxEventStatus.Processed
            },
            cancellationToken: cancellationToken));
    }

    private async Task MarkFailedAsync(IDbConnection connection, OutboxEventRow row, string error, CancellationToken cancellationToken)
    {
        var retryCount = row.RetryCount + 1;
        var status = retryCount >= _options.MaxRetries
            ? OutboxEventStatus.Failed
            : OutboxEventStatus.Pending;

        var seconds = Math.Min(300, _options.RetryBaseDelaySeconds * (int)Math.Pow(2, Math.Min(6, retryCount)));
        var nextAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Core.OutboxEvent
              SET Status = @Status,
                  RetryCount = @RetryCount,
                  NextAttemptUtc = @NextAttemptUtc,
                  LockedUntilUtc = NULL,
                  LastError = @LastError
              WHERE Id = @EventId;",
            new
            {
                EventId = row.Id,
                Status = status,
                RetryCount = retryCount,
                NextAttemptUtc = nextAttemptUtc,
                LastError = error.Length > 1900 ? error[..1900] : error
            },
            cancellationToken: cancellationToken));

        RetriedCounter.Add(1);
        _logger.LogWarning(
            "Falha ao processar Outbox EventId={EventId} CorrelationId={CorrelationId} Retry={RetryCount} Status={Status}. Erro={Error}",
            row.Id,
            row.CorrelationId,
            retryCount,
            status,
            error);
    }

    private static object DeserializePayload(OutboxEventRow row, Type payloadType)
    {
        var payload = JsonSerializer.Deserialize(row.Payload, payloadType);
        if (payload is null)
            throw new InvalidOperationException($"Payload nao pode ser desserializado para tipo {payloadType.Name}.");
        return payload;
    }

    private sealed class OutboxEventRow
    {
        public Guid Id { get; set; }
        public int OrganizacaoId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public Guid? CorrelationId { get; set; }
        public int RetryCount { get; set; }
    }
}
