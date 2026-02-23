using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ProformaFarm.Application.Interfaces.Outbox;
using ProformaFarm.Domain.Events.Outbox;

namespace ProformaFarm.Infrastructure.Outbox.Handlers;

public sealed class HelloOutboxDomainEventHandler : IOutboxEventHandler
{
    public string EventType => typeof(HelloOutboxDomainEvent).FullName!;
    public Type PayloadType => typeof(HelloOutboxDomainEvent);
    public string HandlerName => nameof(HelloOutboxDomainEventHandler);

    public async Task HandleAsync(
        object payload,
        OutboxProcessContext context,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var evt = payload as HelloOutboxDomainEvent
            ?? throw new InvalidOperationException("Payload invalido para HelloOutboxDomainEventHandler.");

        if (evt.SimularFalhaUmaVez && context.RetryCount == 0)
            throw new InvalidOperationException("Falha simulada para validar retry/backoff do Outbox.");

        await connection.ExecuteAsync(new CommandDefinition(
            connection.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
                ? @"UPDATE ""Core"".""OutboxHelloProbe""
                    SET ""ProcessedCount"" = ""ProcessedCount"" + 1,
                        ""UltimoProcessamentoUtc"" = CURRENT_TIMESTAMP
                    WHERE ""IdOutboxHelloProbe"" = @IdOutboxHelloProbe
                      AND ""OrganizacaoId"" = @OrganizacaoId;"
                : @"UPDATE Core.OutboxHelloProbe
                    SET ProcessedCount = ProcessedCount + 1,
                        UltimoProcessamentoUtc = SYSUTCDATETIME()
                    WHERE IdOutboxHelloProbe = @IdOutboxHelloProbe
                      AND OrganizacaoId = @OrganizacaoId;",
            new
            {
                evt.IdOutboxHelloProbe,
                OrganizacaoId = context.OrganizacaoId
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}
