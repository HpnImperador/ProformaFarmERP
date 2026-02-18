using Dapper;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Domain.Entities;
using ProformaFarm.Infrastructure.Data;
using System.Linq;

namespace ProformaFarm.Infrastructure.Repositories.Auth;

public sealed class UserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _factory;

    public UserRepository(ISqlConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Usuario?> GetByIdAsync(int idUsuario)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT TOP 1
    IdUsuario, Nome, Login, SenhaHash, SenhaSalt, Ativo, DataCriacao
FROM Usuario
WHERE IdUsuario = @IdUsuario;
";
        return await cn.QueryFirstOrDefaultAsync<Usuario>(sql, new { IdUsuario = idUsuario });
    }

    public async Task<IReadOnlyList<string>> GetPerfisAsync(int idUsuario)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT p.Nome
FROM UsuarioPerfil up
JOIN Perfil p ON p.IdPerfil = up.IdPerfil
WHERE up.IdUsuario = @IdUsuario
ORDER BY p.Nome;
";
        var rows = await cn.QueryAsync<string>(sql, new { IdUsuario = idUsuario });
        return rows.ToList();
    }

    public async Task<Usuario?> GetByLoginAsync(string login)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT TOP 1
    IdUsuario, Nome, Login, SenhaHash, SenhaSalt, Ativo, DataCriacao
FROM Usuario
WHERE Login = @Login;
";
        return await cn.QueryFirstOrDefaultAsync<Usuario>(sql, new { Login = login });
    }
}