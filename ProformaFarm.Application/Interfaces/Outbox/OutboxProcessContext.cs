using System;

namespace ProformaFarm.Application.Interfaces.Outbox;

public sealed class OutboxProcessContext
{
    public Guid EventId { get; init; }
    public Guid? CorrelationId { get; init; }
    public int OrganizacaoId { get; init; }
    public int RetryCount { get; init; }
}
