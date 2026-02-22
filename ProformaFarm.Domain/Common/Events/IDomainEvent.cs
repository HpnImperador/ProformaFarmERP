using System;

namespace ProformaFarm.Domain.Common.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
    Guid? CorrelationId { get; }
    int OrganizacaoId { get; }
}
