using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Application.Interfaces.Integration;

public interface IEventRelayProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
