using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Domain.Entities;

namespace ProformaFarm.Infrastructure.Repositories.Auth;

public sealed class UserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _factory;

    public UserRepository(ISqlConnectionFactory factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task<Usuario?> GetByLoginAsync(string login, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Login é obrigatório.", nameof(login));

        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT TOP (1)
    IdUsuario, Nome, Login, SenhaHash, SenhaSalt, Ativo, DataCriacao
FROM dbo.Usuario
WHERE Login = @Login;
";
        return await cn.QueryFirstOrDefaultAsync<Usuario>(
            new CommandDefinition(sql, new { Login = login }, cancellationToken: ct));
    }

    public async Task<string[]> GetPerfisAsync(int idUsuario, CancellationToken ct = default)
    {
        if (idUsuario <= 0) throw new ArgumentOutOfRangeException(nameof(idUsuario));

        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT p.Nome
FROM dbo.UsuarioPerfil up
JOIN dbo.Perfil p ON p.IdPerfil = up.IdPerfil
WHERE up.IdUsuario = @IdUsuario;
";
        var perfis = await cn.QueryAsync<string>(
            new CommandDefinition(sql, new { IdUsuario = idUsuario }, cancellationToken: ct));

        return perfis.AsList().ToArray();
    }
}
