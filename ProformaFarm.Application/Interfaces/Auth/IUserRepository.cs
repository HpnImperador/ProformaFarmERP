using ProformaFarm.Domain.Entities;

namespace ProformaFarm.Application.Interfaces.Auth;

public interface IUserRepository
{
    Task<Usuario?> GetByLoginAsync(string login);
    Task<Usuario?> GetByIdAsync(int idUsuario);   // <-- ADICIONAR
    Task<IReadOnlyList<string>> GetPerfisAsync(int idUsuario);
}
