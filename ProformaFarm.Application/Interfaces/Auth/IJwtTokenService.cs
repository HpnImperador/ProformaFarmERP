using ProformaFarm.Application.DTOs.Auth;
using ProformaFarm.Domain.Entities;

namespace ProformaFarm.Application.Interfaces.Auth;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(Usuario usuario, string[] perfis);
}
