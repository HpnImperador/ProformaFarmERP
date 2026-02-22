using System;

namespace ProformaFarm.Infrastructure.Outbox;

public sealed class OutboxEventEntity
{
    public Guid Id { get; set; }
    public int OrganizacaoId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredOnUtc { get; set; }
    public Guid? CorrelationId { get; set; }
    public byte Status { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset NextAttemptUtc { get; set; }
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public string? LastError { get; set; }
}
