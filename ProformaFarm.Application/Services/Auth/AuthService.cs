using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.Common.Exceptions;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Services.Security;

namespace ProformaFarm.Application.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordService _passwords;
    private readonly IJwtTokenService _jwt;

    public AuthService(IUserRepository users, IPasswordService passwords, IJwtTokenService jwt)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _passwords = passwords ?? throw new ArgumentNullException(nameof(passwords));
        _jwt = jwt ?? throw new ArgumentNullException(nameof(jwt));
    }

    public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest req, string? ip, CancellationToken ct = default)
    {
        if (req is null)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["request"] = ["Dados de login ausentes."]
            });
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Login))
            errors["Login"] = ["Login e obrigatorio."];

        if (string.IsNullOrWhiteSpace(req.Senha))
            errors["Senha"] = ["Senha e obrigatoria."];

        if (errors.Count > 0)
            throw new ValidationException(errors);

        var login = req.Login.Trim();
        var usuario = await _users.GetByLoginAsync(login, ct);

        if (usuario is null || !usuario.Ativo)
            return ApiResponse<LoginResponse>.Fail("Usuario ou senha invalidos.", "UNAUTHORIZED");

        var senhaValida = _passwords.VerifyPassword(req.Senha ?? string.Empty, usuario.SenhaHash!, usuario.SenhaSalt!);
        if (!senhaValida)
            return ApiResponse<LoginResponse>.Fail("Usuario ou senha invalidos.", "UNAUTHORIZED");

        var perfis = await _users.GetPerfisAsync(usuario.IdUsuario, ct);
        var tokenResult = _jwt.CreateToken(usuario, perfis);

        return ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = tokenResult.AccessToken,
            ExpiresAtUtc = tokenResult.ExpiresAtUtc.UtcDateTime,
            Perfis = perfis
        });
    }

    public async Task<ApiResponse<LoginResponse>> RefreshAsync(RefreshRequest req, string? ip, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return ApiResponse<LoginResponse>.Fail("Funcionalidade Refresh ainda nao integrada.", "NOT_IMPLEMENTED");
    }

    public async Task<ApiResponse<object>> LogoutAsync(LogoutRequest req, string? ip, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return ApiResponse.Ok("Logout realizado com sucesso.");
    }
}
