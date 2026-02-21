using System;

namespace ProformaFarm.Application.DTOs.Auth;

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public string[] Perfis { get; set; } = Array.Empty<string>();
}

