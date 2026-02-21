using System;
using System.Threading;
using System.Threading.Tasks;
using ProformaFarm.Domain.Entities;

namespace ProformaFarm.Application.Interfaces.Auth;

public interface IRefreshTokenRepository
{
    Task InsertAsync(int idUsuario, string hash, DateTime expiresAtUtc, string? ip, CancellationToken ct = default);

    Task<RefreshTokenRecord?> GetByHashAsync(string hash, CancellationToken ct = default);

    Task RevokeAsync(int idRefreshToken, DateTime revokedAtUtc, string? ip, string? replacedByHash, CancellationToken ct = default);
}
