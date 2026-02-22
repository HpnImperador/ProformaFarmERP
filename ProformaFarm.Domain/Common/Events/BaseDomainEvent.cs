using System;

namespace ProformaFarm.Domain.Common.Events;

public abstract class BaseDomainEvent : IDomainEvent
{
    protected BaseDomainEvent(int organizacaoId, Guid? correlationId = null)
    {
        if (organizacaoId <= 0)
            throw new ArgumentOutOfRangeException(nameof(organizacaoId), "OrganizacaoId deve ser maior que zero.");

        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTimeOffset.UtcNow;
        CorrelationId = correlationId;
        OrganizacaoId = organizacaoId;
    }

    public Guid EventId { get; }
    public DateTimeOffset OccurredOnUtc { get; }
    public Guid? CorrelationId { get; }
    public int OrganizacaoId { get; }
}
