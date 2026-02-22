using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Application.Interfaces.Outbox;

public interface IOutboxHelloService
{
    Task<OutboxHelloResult> EnqueueHelloEventAsync(string? nomeEvento, bool simularFalhaUmaVez, CancellationToken cancellationToken = default);
}

public sealed class OutboxHelloResult
{
    public Guid IdOutboxHelloProbe { get; init; }
    public Guid EventId { get; init; }
    public int OrganizacaoId { get; init; }
    public string NomeEvento { get; init; } = string.Empty;
    public bool SimularFalhaUmaVez { get; init; }
}
