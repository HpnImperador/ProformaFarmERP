using System;
using ProformaFarm.Domain.Common.Events;

namespace ProformaFarm.Domain.Events.Outbox;

public sealed class HelloOutboxDomainEvent : BaseDomainEvent
{
    public HelloOutboxDomainEvent(
        int organizacaoId,
        Guid idOutboxHelloProbe,
        string nomeEvento,
        bool simularFalhaUmaVez,
        Guid? correlationId = null)
        : base(organizacaoId, correlationId)
    {
        IdOutboxHelloProbe = idOutboxHelloProbe;
        NomeEvento = nomeEvento;
        SimularFalhaUmaVez = simularFalhaUmaVez;
    }

    public Guid IdOutboxHelloProbe { get; }
    public string NomeEvento { get; }
    public bool SimularFalhaUmaVez { get; }
}
