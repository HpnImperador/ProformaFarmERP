namespace ProformaFarm.Application.DTOs.Auth;

public sealed class LoginRequest
{
    public string Login { get; set; } = default!;
    public string Senha { get; set; } = default!;
}
