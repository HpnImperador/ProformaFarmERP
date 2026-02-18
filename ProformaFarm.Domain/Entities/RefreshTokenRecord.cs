namespace ProformaFarm.Domain.Entities;

public sealed class RefreshTokenRecord
{
    public int IdRefreshToken { get; set; }
    public int IdUsuario { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime CriadoEmUtc { get; set; }
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? RevogadoEmUtc { get; set; }
    public string? CriadoPorIp { get; set; }
    public string? RevogadoPorIp { get; set; }
    public string? SubstituidoPorHash { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiraEmUtc;
    public bool IsRevoked => RevogadoEmUtc.HasValue;
}
