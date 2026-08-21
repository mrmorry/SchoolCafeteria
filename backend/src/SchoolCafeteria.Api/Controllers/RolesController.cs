using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/roles")]
[RequirePermission("users.manage")]
public class RolesController : ApiControllerBase
{
    private readonly RoleService _service;
    public RolesController(RoleService service) => _service = service;

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetPermissionCatalog(CancellationToken ct)
        => Ok(await _service.GetPermissionCatalogAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken ct)
        => Ok(await _service.GetRolesAsync(SchoolId, ct));

    [HttpPost]
    public async Task<ActionResult<RoleDto>> CreateRole(CreateRoleRequest request, CancellationToken ct)
        => Ok(await _service.CreateRoleAsync(SchoolId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid id, UpdateRoleRequest request, CancellationToken ct)
        => Ok(await _service.UpdateRoleAsync(SchoolId, id, request, ct));

    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult<RoleDto>> SetPermissions(Guid id, SetRolePermissionsRequest request, CancellationToken ct)
        => Ok(await _service.SetPermissionsAsync(SchoolId, id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct)
    {
        await _service.DeleteRoleAsync(SchoolId, id, ct);
        return NoContent();
    }
}
