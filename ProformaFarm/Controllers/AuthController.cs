using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProformaFarm.Application.Common.Responses;
using ProformaFarm.Application.Interfaces.Auth;
using ProformaFarm.Application.Services.Auth;
using LoginRequestDto = ProformaFarm.Application.DTOs.Auth.LoginRequest;
using LoginResponseDto = ProformaFarm.Application.DTOs.Auth.LoginResponse;
using LogoutRequestDto = ProformaFarm.Application.DTOs.Auth.LogoutRequest;
using RefreshRequestDto = ProformaFarm.Application.DTOs.Auth.RefreshRequest;

namespace ProformaFarm.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequestDto req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(req, ip);

        var correlationId = Response.Headers["X-Correlation-Id"].ToString();
        return Ok(ApiResponse<LoginResponseDto>.Ok(result, "Login successful", correlationId));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Refresh([FromBody] RefreshRequestDto req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.RefreshAsync(req, ip);

        var correlationId = Response.Headers["X-Correlation-Id"].ToString();
        return Ok(ApiResponse<LoginResponseDto>.Ok(result, "Token refreshed", correlationId));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout([FromBody] LogoutRequestDto req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auth.LogoutAsync(req, ip);

        var correlationId = Response.Headers["X-Correlation-Id"].ToString();
        return Ok(ApiResponse.Ok("Logout successful", correlationId));
    }
}
