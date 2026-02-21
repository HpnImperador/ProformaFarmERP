using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace ProformaFarm.Controllers;

[ApiController]
[Route("api/_debug")]
public class DebugController : ControllerBase
{
    [HttpGet("admin-only")]
    [Authorize(Roles = "ADMIN")]
    public IActionResult AdminOnly() => Ok("ok");

}
