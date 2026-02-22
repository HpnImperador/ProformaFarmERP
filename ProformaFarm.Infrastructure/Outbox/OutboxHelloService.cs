using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProformaFarm.Application.Interfaces.Context;
using ProformaFarm.Application.Interfaces.Correlation;
using ProformaFarm.Application.Interfaces.Outbox;
using ProformaFarm.Domain.Events.Outbox;
using ProformaFarm.Domain.Entities;
using ProformaFarm.Infrastructure.Data;

namespace ProformaFarm.Infrastructure.Outbox;

public sealed class OutboxHelloService : IOutboxHelloService
{
    private readonly ProformaFarmDbContext _dbContext;
    private readonly IOrgContext _orgContext;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public OutboxHelloService(
        ProformaFarmDbContext dbContext,
        IOrgContext orgContext,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<OutboxHelloResult> EnqueueHelloEventAsync(string? nomeEvento, bool simularFalhaUmaVez, CancellationToken cancellationToken = default)
    {
        var idOrganizacao = await _orgContext.GetCurrentOrganizacaoIdAsync(cancellationToken);
        if (!idOrganizacao.HasValue)
            throw new InvalidOperationException("Contexto organizacional nao resolvido para o usuario.");

        var correlationId = _correlationIdAccessor.GetCurrentCorrelationId();
        var probe = OutboxHelloProbe.Create(
            organizacaoId: idOrganizacao.Value,
            nomeEvento: string.IsNullOrWhiteSpace(nomeEvento) ? "HELLO_OUTBOX" : nomeEvento!,
            simularFalhaUmaVez: simularFalhaUmaVez,
            correlationId: correlationId);

        var eventId = probe.DomainEvents.OfType<HelloOutboxDomainEvent>().Single().EventId;

        _dbContext.OutboxHelloProbes.Add(probe);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OutboxHelloResult
        {
            IdOutboxHelloProbe = probe.IdOutboxHelloProbe,
            EventId = eventId,
            OrganizacaoId = probe.OrganizacaoId,
            NomeEvento = probe.NomeEvento,
            SimularFalhaUmaVez = probe.SimularFalhaUmaVez
        };
    }
}
