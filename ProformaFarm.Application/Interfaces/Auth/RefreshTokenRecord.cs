using System;
using System.Collections.Generic;
using System.Text;

namespace ProformaFarm.Application.Interfaces.Auth;

public sealed class RefreshTokenRecord
{
    public int IdRefreshToken { get; init; }
    public int IdUsuario { get; init; }
    public string Hash { get; init; } = default!;
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; init; }

    public bool IsRevoked => RevokedAtUtc.HasValue;
    public bool IsExpired => ExpiresAtUtc <= DateTime.UtcNow;
}

