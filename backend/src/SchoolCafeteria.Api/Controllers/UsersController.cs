using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/users")]
[RequirePermission("users.manage")]
public class UsersController : ApiControllerBase
{
    private readonly UserAdminService _service;
    public UsersController(UserAdminService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserSummaryDto>>> Search([FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _service.SearchAsync(SchoolId, request, ct));

    [HttpPost]
    public async Task<ActionResult<UserSummaryDto>> Create(CreateInternalUserRequest request, CancellationToken ct)
        => Ok(await _service.CreateInternalUserAsync(SchoolId, request, ct));

    [HttpPost("roles")]
    public async Task<IActionResult> AssignRole(AssignUserRoleRequest request, CancellationToken ct)
    {
        await _service.AssignRoleAsync(SchoolId, request, ct);
        return NoContent();
    }

    [HttpDelete("{userId:guid}/roles/{userRoleId:guid}")]
    public async Task<IActionResult> RemoveRole(Guid userId, Guid userRoleId, CancellationToken ct)
    {
        await _service.RemoveRoleAsync(SchoolId, userId, userRoleId, ct);
        return NoContent();
    }

    [HttpPut("{userId:guid}/active")]
    public async Task<IActionResult> SetActive(Guid userId, SetUserActiveRequest request, CancellationToken ct)
    {
        await _service.SetActiveAsync(SchoolId, userId, request, ct);
        return NoContent();
    }
}
