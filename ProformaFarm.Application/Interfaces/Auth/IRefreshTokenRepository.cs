using System;
using System.Collections.Generic;
using System.Text;
using ProformaFarm.Domain.Entities;

namespace ProformaFarm.Application.Interfaces.Auth;

public interface IRefreshTokenRepository
{
    Task InsertAsync(int idUsuario, string hash, DateTime expiresAtUtc, string? ip);
    Task<RefreshTokenRecord?> GetByHashAsync(string hash);
    Task RevokeAsync(int idRefreshToken, DateTime revokedAtUtc, string? ip, string? replacedByHash);
}


