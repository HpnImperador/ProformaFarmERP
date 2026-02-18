using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProformaFarm.Domain.Entities;

namespace ProformaFarm.Application.Services.Auth;

public interface IJwtTokenService
{
    (string token, DateTime expiresAtUtc) CreateToken(Usuario usuario, string[] perfis);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string token, DateTime expiresAtUtc) CreateToken(Usuario usuario, string[] perfis)
    {
        var issuer = _config["Jwt:Issuer"] ?? "ProformaFarm";
        var audience = _config["Jwt:Audience"] ?? "ProformaFarm";
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não configurado.");

        var expires = DateTime.UtcNow.AddHours(8);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, usuario.Login),
            new("nome", usuario.Nome),
            new("uid", usuario.IdUsuario.ToString()),
        };

        foreach (var p in perfis)
            claims.Add(new Claim(ClaimTypes.Role, p));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return (token, expires);
    }
}
