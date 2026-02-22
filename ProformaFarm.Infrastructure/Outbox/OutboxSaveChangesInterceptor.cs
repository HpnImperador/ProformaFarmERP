using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProformaFarm.Application.Interfaces.Correlation;
using ProformaFarm.Domain.Common.Entities;
using ProformaFarm.Infrastructure.Data;

namespace ProformaFarm.Infrastructure.Outbox;

public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public OutboxSaveChangesInterceptor(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendOutboxEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendOutboxEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendOutboxEvents(DbContext? context)
    {
        if (context is not ProformaFarmDbContext dbContext)
            return;

        var entities = dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .ToList();

        if (entities.Count == 0)
            return;

        var fallbackCorrelationId = _correlationIdAccessor.GetCurrentCorrelationId();

        foreach (var entry in entities)
        {
            var events = entry.Entity.DomainEvents.ToList();
            foreach (var domainEvent in events)
            {
                if (domainEvent.OrganizacaoId <= 0)
                    throw new InvalidOperationException($"Domain event {domainEvent.GetType().Name} sem OrganizacaoId valido.");

                var outbox = new OutboxEventEntity
                {
                    Id = domainEvent.EventId,
                    OrganizacaoId = domainEvent.OrganizacaoId,
                    EventType = domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredOnUtc = domainEvent.OccurredOnUtc,
                    CorrelationId = domainEvent.CorrelationId ?? fallbackCorrelationId,
                    Status = OutboxEventStatus.Pending,
                    RetryCount = 0,
                    NextAttemptUtc = DateTimeOffset.MinValue
                };

                dbContext.OutboxEvents.Add(outbox);
            }

            entry.Entity.ClearDomainEvents();
        }
    }
}

