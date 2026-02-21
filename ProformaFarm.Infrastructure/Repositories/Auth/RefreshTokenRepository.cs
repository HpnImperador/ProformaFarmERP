using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Interfaces.Data;
using ProformaFarm.Infrastructure.Data;

namespace ProformaFarm.Infrastructure.Repositories.Auth;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ISqlConnectionFactory _factory;

    public RefreshTokenRepository(ISqlConnectionFactory factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public async Task InsertAsync(
        int idUsuario,
        string hash,
        DateTime expiresAtUtc,
        string? ip,
        CancellationToken ct = default)
    {
        if (idUsuario <= 0) throw new ArgumentOutOfRangeException(nameof(idUsuario));
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash do refresh token é obrigatório.", nameof(hash));

        if (expiresAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("expiresAtUtc deve estar em UTC.", nameof(expiresAtUtc));

        using var cn = _factory.CreateConnection();

        const string sql = @"
INSERT INTO dbo.RefreshToken
    (IdUsuario, TokenHash, ExpiraEmUtc, CriadoEmUtc, CriadoPorIp)
VALUES
    (@IdUsuario, @TokenHash, @ExpiraEmUtc, SYSUTCDATETIME(), @CriadoPorIp);
";

        var cmd = new CommandDefinition(sql, new
        {
            IdUsuario = idUsuario,
            TokenHash = hash,
            ExpiraEmUtc = expiresAtUtc,
            CriadoPorIp = ip
        }, cancellationToken: ct);

        await cn.ExecuteAsync(cmd);
    }

    public async Task<RefreshTokenRecord?> GetByHashAsync(string hash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash do refresh token é obrigatório.", nameof(hash));

        using var cn = _factory.CreateConnection();

        // Observação: aqui eu retorno apenas token "ativo" (não revogado e não expirado).
        // Se você quiser buscar mesmo revogado/expirado para auditoria, crie outro método (ex.: GetAnyByHashAsync).
        const string sql = @"
SELECT TOP (1)
    IdRefreshToken,
    IdUsuario,
    TokenHash,
    CriadoEmUtc,
    ExpiraEmUtc,
    RevogadoEmUtc,
    CriadoPorIp,
    RevogadoPorIp,
    SubstituidoPorHash
FROM dbo.RefreshToken
WHERE TokenHash = @TokenHash
  AND RevogadoEmUtc IS NULL
  AND ExpiraEmUtc > SYSUTCDATETIME()
ORDER BY IdRefreshToken DESC;
";

        var cmd = new CommandDefinition(sql, new { TokenHash = hash }, cancellationToken: ct);
        return await cn.QueryFirstOrDefaultAsync<RefreshTokenRecord>(cmd);
    }

    public async Task RevokeAsync(
        int idRefreshToken,
        DateTime revokedAtUtc,
        string? ip,
        string? replacedByHash,
        CancellationToken ct = default)
    {
        if (idRefreshToken <= 0)
            throw new ArgumentOutOfRangeException(nameof(idRefreshToken));

        if (revokedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("revokedAtUtc deve estar em UTC.", nameof(revokedAtUtc));

        using var cn = _factory.CreateConnection();

        const string sql = @"
UPDATE dbo.RefreshToken
SET
    RevogadoEmUtc      = @RevogadoEmUtc,
    RevogadoPorIp      = @RevogadoPorIp,
    SubstituidoPorHash = @SubstituidoPorHash
WHERE IdRefreshToken = @IdRefreshToken
  AND RevogadoEmUtc IS NULL;
";

        var cmd = new CommandDefinition(sql, new
        {
            IdRefreshToken = idRefreshToken,
            RevogadoEmUtc = revokedAtUtc,
            RevogadoPorIp = ip,
            SubstituidoPorHash = replacedByHash
        }, cancellationToken: ct);

        var affected = await cn.ExecuteAsync(cmd);

        if (affected == 0)
            throw new InvalidOperationException(
                $"RefreshToken {idRefreshToken} inexistente ou já revogado.");
    }

    public Task InsertAsync(int idUsuario, string hash, DateTime expiresAtUtc, string ip)
    {
        throw new NotImplementedException();
    }

    public Task<RefreshTokenRecord> GetByHashAsync(string hash)
    {
        throw new NotImplementedException();
    }

    public Task RevokeAsync(int idRefreshToken, DateTime revokedAtUtc, string ip, string replacedByHash)
    {
        throw new NotImplementedException();
    }
}
