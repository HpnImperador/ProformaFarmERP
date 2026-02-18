using Dapper;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Infrastructure.Data;

namespace ProformaFarm.Infrastructure.Repositories.Auth;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ISqlConnectionFactory _factory;

    public RefreshTokenRepository(ISqlConnectionFactory factory)
        => _factory = factory;

    public async Task InsertAsync(int idUsuario, string hashBytes, DateTime expiresAtUtc, string? ip)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
INSERT INTO dbo.RefreshToken (UserId, TokenHash, ExpiresAtUtc, CreatedAtUtc, CreatedIp)
VALUES (@UserId, @TokenHash, @ExpiresAtUtc, SYSUTCDATETIME(), @CreatedIp);
";

        await cn.ExecuteAsync(sql, new
        {
            UserId = idUsuario,
            TokenHash = hashBytes,         // byte[] de 32 bytes
            ExpiresAtUtc = expiresAtUtc,
            CreatedIp = ip
        });
    }

    public async Task<RefreshTokenRecord?> GetByHashAsync(string hash)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
SELECT TOP 1
    IdRefreshToken,
    IdUsuario,
    Hash,
    ExpiresAtUtc,
    RevokedAtUtc
FROM dbo.RefreshToken
WHERE Hash = @Hash
ORDER BY IdRefreshToken DESC;
";
        return await cn.QueryFirstOrDefaultAsync<RefreshTokenRecord>(sql, new { Hash = hash });
    }

    public async Task RevokeAsync(int idRefreshToken, DateTime revokedAtUtc, string? ip, string? replacedByHash)
    {
        using var cn = _factory.CreateConnection();

        const string sql = @"
UPDATE dbo.RefreshToken
SET
    RevokedAtUtc = @RevokedAtUtc,
    RevokedIp = @RevokedIp,
    ReplacedByHash = @ReplacedByHash
WHERE IdRefreshToken = @IdRefreshToken
  AND RevokedAtUtc IS NULL;
";
        await cn.ExecuteAsync(sql, new
        {
            IdRefreshToken = idRefreshToken,
            RevokedAtUtc = revokedAtUtc,
            RevokedIp = ip,
            ReplacedByHash = replacedByHash
        });
    }
}
