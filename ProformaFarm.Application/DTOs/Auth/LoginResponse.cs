namespace ProformaFarm.Application.DTOs.Auth;

public sealed class LoginResponse
{
    public string Token { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }

    // NOVO
    public string RefreshToken { get; set; } = default!;
    public DateTime RefreshExpiresAtUtc { get; set; }

    public int IdUsuario { get; set; }
    public string Nome { get; set; } = default!;
    public string Login { get; set; } = default!;
    public string[] Perfis { get; set; } = Array.Empty<string>();
}
