using Microsoft.Extensions.Configuration;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Services.Security;
using System.Security.Cryptography;
using System.Text;

namespace ProformaFarm.Application.Services.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest req, string? ip);
    Task<LoginResponse?> RefreshAsync(RefreshRequest req, string? ip);
    Task<bool> LogoutAsync(LogoutRequest req, string? ip);
}

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refresh;
    private readonly IPasswordService _passwords;
    private readonly IJwtTokenService _jwt;
    private readonly int _refreshDays;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refresh,
        IPasswordService passwords,
        IJwtTokenService jwt,
        IConfiguration config)
    {
        _users = users;
        _refresh = refresh;
        _passwords = passwords;
        _jwt = jwt;

        _refreshDays = int.TryParse(config["RefreshToken:Days"], out var d) ? d : 30;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest req, string? ip)
    {
        var user = await _users.GetByLoginAsync(req.Login);
        if (user is null || !user.Ativo)
            return null;

        // ORDEM CERTA: senha digitada, hash do banco, salt do banco
        if (!_passwords.VerifyPassword(req.Senha, user.SenhaHash, user.SenhaSalt))
            return null;

        var perfis = await _users.GetPerfisAsync(user.IdUsuario);
        var (token, exp) = _jwt.CreateToken(user, perfis.ToArray());

        var refreshTokenRaw = GenerateRefreshToken();
        var refreshHash = Sha256Base64(refreshTokenRaw);
        var refreshExp = DateTime.UtcNow.AddDays(_refreshDays);

        await _refresh.InsertAsync(user.IdUsuario, refreshHash, refreshExp, ip);

        return new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = exp,
            RefreshToken = refreshTokenRaw,
            RefreshExpiresAtUtc = refreshExp,
            IdUsuario = user.IdUsuario,
            Nome = user.Nome,
            Login = user.Login,
            Perfis = perfis.ToArray()
        };
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshRequest req, string? ip)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return null;

        var oldHash = Sha256Base64(req.RefreshToken);
        var record = await _refresh.GetByHashAsync(oldHash);
        if (record is null || record.IsRevoked || record.IsExpired)
            return null;

        // Recarrega usuário e perfis (evita token para usuário desativado)
        var user = await _users.GetByIdAsync(record.IdUsuario);
        if (user is null || !user.Ativo)
            return null;

        var perfis = await _users.GetPerfisAsync(user.IdUsuario);
        var (token, exp) = _jwt.CreateToken(user, perfis.ToArray());

        // ROTATION: revoga o refresh anterior e cria um novo
        var newRefreshRaw = GenerateRefreshToken();
        var newHash = Sha256Base64(newRefreshRaw);
        var newExp = DateTime.UtcNow.AddDays(_refreshDays);

        await _refresh.RevokeAsync(record.IdRefreshToken, DateTime.UtcNow, ip, newHash);
        await _refresh.InsertAsync(user.IdUsuario, newHash, newExp, ip);

        return new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = exp,
            RefreshToken = newRefreshRaw,
            RefreshExpiresAtUtc = newExp,
            IdUsuario = user.IdUsuario,
            Nome = user.Nome,
            Login = user.Login,
            Perfis = perfis.ToArray()
        };
    }

    public async Task<bool> LogoutAsync(LogoutRequest req, string? ip)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return false;

        var hash = Sha256Base64(req.RefreshToken);
        var record = await _refresh.GetByHashAsync(hash);
        if (record is null || record.IsRevoked)
            return true; // idempotente

        await _refresh.RevokeAsync(record.IdRefreshToken, DateTime.UtcNow, ip, null);
        return true;
    }

    // ---------------------------
    // Helpers
    // ---------------------------
    private static string GenerateRefreshToken()
    {
        // 64 bytes => token bem forte
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncode(bytes);
    }

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
