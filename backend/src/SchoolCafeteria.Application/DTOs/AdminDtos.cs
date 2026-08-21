namespace SchoolCafeteria.Application.DTOs;

public record PermissionDto(Guid Id, string Key, string Module, string Description);

public record RoleDto(Guid Id, string Name, string? Description, bool IsSystemRole, IReadOnlyList<string> Permissions, int UserCount);

public record CreateRoleRequest(string Name, string? Description);

public record UpdateRoleRequest(string Name, string? Description);

/// <summary>Replaces the full set of permissions assigned to a role — the UI sends the checked
/// state of the whole permission matrix for that role on every save.</summary>
public record SetRolePermissionsRequest(IReadOnlyList<string> PermissionKeys);

public record UserSummaryDto(
    Guid Id, string Email, string FullName, bool IsActive, bool MfaEnabled,
    IReadOnlyList<RoleRefDto> Roles, bool HasEntraLink, DateTime? LastLoginAtUtc);

public record RoleRefDto(Guid Id, string Name);

/// <summary>Creates a login account for internal staff (Administrador, Finanzas, Supervisor,
/// Operador, Auditor). Distinct from Employee/Student — those are compradores, not login accounts.</summary>
public record CreateInternalUserRequest(string Email, string FullName, string TemporaryPassword, IReadOnlyList<Guid> RoleIds);

public record AssignUserRoleRequest(Guid UserId, Guid RoleId, Guid? PointOfSaleId);

public record SetUserActiveRequest(bool IsActive);
