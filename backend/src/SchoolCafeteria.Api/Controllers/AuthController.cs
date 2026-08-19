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
