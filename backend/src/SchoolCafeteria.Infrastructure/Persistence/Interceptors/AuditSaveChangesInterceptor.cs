using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps CreatedAtUtc/UpdatedAtUtc/CreatedBy/UpdatedBy on every IAuditable entity and writes an
/// AuditLog row for each tracked Add/Modify/Delete, all inside the SAME SaveChanges call as the
/// business change — this is what guarantees there is never a gap between an operation and its
/// audit trail. Sensitive properties ([Sensitive]) are excluded from the captured JSON.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser, IDateTimeProvider clock)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Process(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Process(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Process(DbContext context)
    {
        var auditEntries = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is AuditLog) continue; // never audit the audit table itself

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added) auditable.CreatedAtUtc = _clock.UtcNow;
                if (entry.State == EntityState.Modified) auditable.UpdatedAtUtc = _clock.UtcNow;

                if (entry.State == EntityState.Added) auditable.CreatedBy = _currentUser.UserId;
                if (entry.State == EntityState.Modified) auditable.UpdatedBy = _currentUser.UserId;
            }

            var entityName = entry.Entity.GetType().Name;
            var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");

            auditEntries.Add(new AuditLog
            {
                SchoolId = entry.Entity is ISchoolScoped scoped ? scoped.SchoolId : _currentUser.SchoolId,
                UserId = _currentUser.UserId,
                Action = entry.State.ToString(),
                EntityName = entityName,
                EntityId = idProperty?.CurrentValue?.ToString(),
                OldValuesJson = entry.State is EntityState.Modified or EntityState.Deleted ? Serialize(entry.Properties, useOriginal: true) : null,
                NewValuesJson = entry.State is EntityState.Added or EntityState.Modified ? Serialize(entry.Properties, useOriginal: false) : null,
                IpAddress = _currentUser.IpAddress,
                UserAgent = _currentUser.UserAgent,
                OccurredAtUtc = _clock.UtcNow
            });
        }

        foreach (var log in auditEntries)
            context.Add(log);
    }

    private static string Serialize(IEnumerable<PropertyEntry> properties, bool useOriginal)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in properties)
        {
            if (prop.Metadata.PropertyInfo?.GetCustomAttributes(typeof(SensitiveAttribute), true).Any() == true)
                continue; // never persist sensitive values in the audit trail
            if (prop.Metadata.Name is nameof(BaseEntity.RowVersion)) continue;

            dict[prop.Metadata.Name] = useOriginal ? prop.OriginalValue : prop.CurrentValue;
        }
        return JsonSerializer.Serialize(dict);
    }
}
