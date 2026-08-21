using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// Administers roles and their permission assignments. Permission keys are data (see
/// docs/05-roles-permisos.md) — this service is what makes them actually configurable from the
/// UI instead of only from a seed script.
/// </summary>
public class RoleService
{
    private readonly IAppDbContext _db;
    public RoleService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionCatalogAsync(CancellationToken ct = default) =>
        await _db.Permissions.OrderBy(p => p.Module).ThenBy(p => p.Key)
            .Select(p => new PermissionDto(p.Id, p.Key, p.Module, p.Description)).ToListAsync(ct);

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(Guid schoolId, CancellationToken ct = default)
    {
        var roles = await _db.Roles.Where(r => r.SchoolId == schoolId).OrderBy(r => r.Name).ToListAsync(ct);
        var result = new List<RoleDto>();
        foreach (var role in roles) result.Add(await MapAsync(role, ct));
        return result;
    }

    public async Task<RoleDto> CreateRoleAsync(Guid schoolId, CreateRoleRequest request, CancellationToken ct = default)
    {
        var duplicate = await _db.Roles.AnyAsync(r => r.SchoolId == schoolId && r.Name == request.Name, ct);
        if (duplicate) throw new BusinessRuleException("role.duplicate_name", $"Ya existe un rol llamado '{request.Name}'.");

        var role = new Role { SchoolId = schoolId, Name = request.Name, Description = request.Description, IsSystemRole = false };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return await MapAsync(role, ct);
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid schoolId, Guid roleId, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await GetOwnedRoleAsync(schoolId, roleId, ct);
        role.Name = request.Name;
        role.Description = request.Description;
        await _db.SaveChangesAsync(ct);
        return await MapAsync(role, ct);
    }

    /// <summary>Replaces the role's full permission set in one transaction — the caller (the admin
    /// UI) always submits the whole checked/unchecked matrix for the role, never a delta.</summary>
    public async Task<RoleDto> SetPermissionsAsync(Guid schoolId, Guid roleId, SetRolePermissionsRequest request, CancellationToken ct = default)
    {
        var role = await GetOwnedRoleAsync(schoolId, roleId, ct);

        var requestedKeys = request.PermissionKeys.Distinct().ToList();
        var validPermissions = await _db.Permissions.Where(p => requestedKeys.Contains(p.Key)).ToListAsync(ct);
        if (validPermissions.Count != requestedKeys.Count)
        {
            var unknown = requestedKeys.Except(validPermissions.Select(p => p.Key));
            throw new BusinessRuleException("role.unknown_permission", $"Permiso(s) desconocido(s): {string.Join(", ", unknown)}.");
        }

        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        var existingKeys = existing.Select(rp => rp.PermissionId).ToHashSet();
        var validIds = validPermissions.Select(p => p.Id).ToHashSet();

        foreach (var toRemove in existing.Where(rp => !validIds.Contains(rp.PermissionId)))
            _db.RolePermissions.Remove(toRemove);

        foreach (var permission in validPermissions.Where(p => !existingKeys.Contains(p.Id)))
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });

        await _db.SaveChangesAsync(ct);
        return await MapAsync(role, ct);
    }

    public async Task DeleteRoleAsync(Guid schoolId, Guid roleId, CancellationToken ct = default)
    {
        var role = await GetOwnedRoleAsync(schoolId, roleId, ct);
        if (role.IsSystemRole)
            throw new BusinessRuleException("role.system_role", "Los roles predefinidos del sistema no pueden eliminarse.");

        var hasUsers = await _db.UserRoles.AnyAsync(ur => ur.RoleId == roleId, ct);
        if (hasUsers)
            throw new BusinessRuleException("role.has_users", "No se puede eliminar un rol que tiene usuarios asignados. Reasigne los usuarios primero.");

        var rolePermissions = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(rolePermissions);
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Role> GetOwnedRoleAsync(Guid schoolId, Guid roleId, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(Role), roleId);
        return role;
    }

    private async Task<RoleDto> MapAsync(Role role, CancellationToken ct)
    {
        var permissionKeys = await (from rp in _db.RolePermissions
                                     join p in _db.Permissions on rp.PermissionId equals p.Id
                                     where rp.RoleId == role.Id
                                     select p.Key).ToListAsync(ct);
        var userCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == role.Id, ct);
        return new RoleDto(role.Id, role.Name, role.Description, role.IsSystemRole, permissionKeys, userCount);
    }
}
