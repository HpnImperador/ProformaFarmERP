using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;

namespace ProformaFarm.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthMeController : ControllerBase
{
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var idUsuario = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var login = User.FindFirstValue(ClaimTypes.Name);
        var perfis = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();

        return Ok(new { idUsuario, login, perfis });
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("admin-only")]
    public IActionResult AdminOnly() => Ok("ok");
}