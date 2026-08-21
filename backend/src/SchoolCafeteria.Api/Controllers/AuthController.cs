using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    public AuthController(AuthService authService) => _authService = authService;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await _authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    /// <summary>Staff login via Microsoft Entra ID — coexists with /login, never replaces it. See
    /// docs/06-runbook.md for how to connect a real Entra ID tenant.</summary>
    [HttpPost("entra-login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> EntraLogin(EntraIdLoginRequest request, CancellationToken ct)
        => Ok(await _authService.LoginWithEntraIdAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Refresh(RefreshRequest request, CancellationToken ct)
        => Ok(await _authService.RefreshAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);
        await _authService.RevokeAllSessionsAsync(userId, ct);
        return NoContent();
    }
}
