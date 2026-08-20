namespace SchoolCafeteria.Domain.Common;

/// <summary>Marker for entities that must be captured by the audit interceptor.</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    string? UpdatedBy { get; set; }
}

/// <summary>Marker for entities that belong to a single school (multi-tenant isolation).</summary>
public interface ISchoolScoped
{
    Guid SchoolId { get; set; }
}

/// <summary>Marker for master data that is deactivated rather than physically removed.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}

/// <summary>Marks a property as sensitive so the audit interceptor and log sanitizer exclude its value.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute
{
}

public abstract class BaseEntity : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public abstract class SchoolScopedEntity : BaseEntity, ISchoolScoped
{
    public Guid SchoolId { get; set; }
}

public abstract class SoftDeletableSchoolEntity : SchoolScopedEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
