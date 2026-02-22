using System;
using ProformaFarm.Domain.Common.Entities;
using ProformaFarm.Domain.Events.Outbox;

namespace ProformaFarm.Domain.Entities;

public sealed class OutboxHelloProbe : AggregateRoot
{
    public Guid IdOutboxHelloProbe { get; private set; }
    public int OrganizacaoId { get; private set; }
    public string NomeEvento { get; private set; } = string.Empty;
    public bool SimularFalhaUmaVez { get; private set; }
    public int ProcessedCount { get; private set; }
    public DateTimeOffset CriadoEmUtc { get; private set; }
    public DateTimeOffset? UltimoProcessamentoUtc { get; private set; }

    private OutboxHelloProbe()
    {
    }

    public static OutboxHelloProbe Create(int organizacaoId, string nomeEvento, bool simularFalhaUmaVez, Guid? correlationId = null)
    {
        if (organizacaoId <= 0)
            throw new ArgumentOutOfRangeException(nameof(organizacaoId), "OrganizacaoId deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(nomeEvento))
            throw new ArgumentException("NomeEvento deve ser informado.", nameof(nomeEvento));

        var probe = new OutboxHelloProbe
        {
            IdOutboxHelloProbe = Guid.NewGuid(),
            OrganizacaoId = organizacaoId,
            NomeEvento = nomeEvento.Trim(),
            SimularFalhaUmaVez = simularFalhaUmaVez,
            ProcessedCount = 0,
            CriadoEmUtc = DateTimeOffset.UtcNow
        };

        probe.AddDomainEvent(new HelloOutboxDomainEvent(
            organizacaoId: probe.OrganizacaoId,
            idOutboxHelloProbe: probe.IdOutboxHelloProbe,
            nomeEvento: probe.NomeEvento,
            simularFalhaUmaVez: probe.SimularFalhaUmaVez,
            correlationId: correlationId));

        return probe;
    }
}
