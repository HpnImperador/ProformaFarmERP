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
    private readonly bool _isPostgres;

    public UserRepository(ISqlConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _isPostgres = factory.ProviderName.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            || factory.ProviderName.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<Usuario?> GetByLoginAsync(string login, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Login é obrigatório.", nameof(login));

        using var cn = _factory.CreateConnection();

        var sql = _isPostgres
            ? @"
SELECT
    ""IdUsuario"", ""Nome"", ""Login"", ""SenhaHash"", ""SenhaSalt"", ""Ativo"", ""DataCriacao""
FROM dbo.usuario
WHERE ""Login"" = @Login
LIMIT 1;
"
            : @"
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

        var sql = _isPostgres
            ? @"
SELECT p.""Nome""
FROM dbo.usuarioperfil up
JOIN dbo.perfil p ON p.""IdPerfil"" = up.""IdPerfil""
WHERE up.""IdUsuario"" = @IdUsuario;
"
            : @"
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
