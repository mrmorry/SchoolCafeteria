using SchoolCafeteria.Domain.Common;

namespace SchoolCafeteria.Domain.Entities;

/// <summary>Append-only. Written by AuditSaveChangesInterceptor in the same transaction as the business change.</summary>
public class AuditLog : BaseEntity
{
    public Guid? SchoolId { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Login, Export, RoleChange...
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? Reason { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Typed key/value configuration scoped per school. Currency, tax handling, thresholds, etc.
/// Never hardcode these values in code.</summary>
public class SystemSetting : SchoolScopedEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = "string"; // string | number | bool | json
    public string? Description { get; set; }
}
