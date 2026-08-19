using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SchoolCafeteria.Application.Common;

namespace SchoolCafeteria.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
    public string? Email => User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? SchoolId
    {
        get
        {
            var value = User?.FindFirstValue("school_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public IReadOnlyCollection<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
    public IReadOnlyCollection<string> Permissions => User?.FindAll("permission").Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public Guid? GuardianId => Guid.TryParse(User?.FindFirstValue("guardian_id"), out var id) ? id : null;
    public Guid? BuyerId => Guid.TryParse(User?.FindFirstValue("buyer_id"), out var id) ? id : null;

    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => _accessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
}
