using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Options;
using ProformaFarm.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace ProformaFarm.Application.Services.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opts;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _opts = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_opts.SigningKey))
            throw new InvalidOperationException("Jwt:SigningKey não configurado.");
        if (string.IsNullOrWhiteSpace(_opts.Issuer))
            throw new InvalidOperationException("Jwt:Issuer não configurado.");
        if (string.IsNullOrWhiteSpace(_opts.Audience))
            throw new InvalidOperationException("Jwt:Audience não configurado.");
    }

    public JwtTokenResult CreateToken(Usuario usuario, string[] perfis)
    {
        if (usuario is null) throw new ArgumentNullException(nameof(usuario));
        perfis ??= Array.Empty<string>();

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, usuario.Login ?? string.Empty),
            new("nome", usuario.Nome ?? string.Empty)
        };

        foreach (var p in perfis.Where(x => !string.IsNullOrWhiteSpace(x)))
            claims.Add(new Claim(ClaimTypes.Role, p));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtTokenResult(accessToken, expires);
    }
}
