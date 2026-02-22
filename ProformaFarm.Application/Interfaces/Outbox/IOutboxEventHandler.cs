using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Application.Interfaces.Outbox;

public interface IOutboxEventHandler
{
    string EventType { get; }
    Type PayloadType { get; }
    string HandlerName { get; }

    Task HandleAsync(
        object payload,
        OutboxProcessContext context,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken);
}
