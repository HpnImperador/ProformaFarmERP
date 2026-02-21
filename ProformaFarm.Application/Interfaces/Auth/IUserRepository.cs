using ProformaFarm.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Application.Interfaces.Auth;

public interface IUserRepository
{
    Task<Usuario?> GetByLoginAsync(string login, CancellationToken ct = default);
    Task<string[]> GetPerfisAsync(int idUsuario, CancellationToken ct = default);
}
