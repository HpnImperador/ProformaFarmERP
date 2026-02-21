using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.DTOs.Auth; // DTOs reais
using System.Threading.Tasks;

namespace ProformaFarm.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Agora o compilador saberá que 'result' NÃO é void
        var result = await _auth.LoginAsync(req, ip);

        if (!result.Success)
            return Unauthorized(result);

        return Ok(result);
    }

    // ... outros métodos seguindo o mesmo padrão ...
}