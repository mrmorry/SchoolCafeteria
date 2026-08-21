using SchoolCafeteria.Domain.Common;

namespace SchoolCafeteria.Domain.Entities;

/// <summary>Root tenant. v1 operates a single school but every scoped entity already carries SchoolId.</summary>
public class School : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string LegalId { get; set; } = string.Empty;
    public string DefaultCurrency { get; set; } = "USD";
    public string DefaultLocale { get; set; } = "es";
    public string TimeZoneId { get; set; } = "America/Panama";
    public bool IsActive { get; set; } = true;
}

public class User : SchoolScopedEntity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    [Sensitive]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }

    [Sensitive]
    public string? MfaSecret { get; set; }

    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>
    /// Microsoft Entra ID object id (the "oid" claim), set the first time this account signs in
    /// via Entra ID. Authentication can happen through Entra ID or through the local
    /// email/password flow — either way, authorization always flows through this same User's
    /// UserRole/RolePermission rows, so permissions never depend on which login method was used.
    /// </summary>
    public string? EntraObjectId { get; set; }

    /// <summary>Links to Guardian, Employee or Student when the user is that person's login.</summary>
    public Guid? GuardianId { get; set; }
    public Guardian? Guardian { get; set; }
    public Guid? BuyerId { get; set; }
    public Buyer? Buyer { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Sensitive]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
}

/// <summary>Role names are data, not hardcoded switches — authorization checks permissions, not role names.</summary>
public class Role : SchoolScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission : BaseEntity
{
    /// <summary>Stable machine key, e.g. "wallet.adjust", "sales.refund".</summary>
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

public class UserRole : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }

    /// <summary>Optional scoping to specific points of sale for the "operador solo opera cajas autorizadas" rule.</summary>
    public Guid? PointOfSaleId { get; set; }
}
