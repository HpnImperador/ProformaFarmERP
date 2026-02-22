using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Application.Interfaces.Outbox;

public interface IOutboxProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
