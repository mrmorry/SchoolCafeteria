using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Services;

/// <summary>Read-only query surface for the Auditor role. No write methods exist on this service by design
/// (rule: un auditor no puede modificar información).</summary>
public class AuditService
{
    private readonly IAppDbContext _db;
    public AuditService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<AuditLog>> SearchAsync(
        Guid? schoolId, string? entityName, string? userId, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (schoolId.HasValue) query = query.Where(a => a.SchoolId == schoolId);
        if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(a => a.EntityName == entityName);
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(a => a.UserId == userId);
        if (fromUtc.HasValue) query = query.Where(a => a.OccurredAtUtc >= fromUtc);
        if (toUtc.HasValue) query = query.Where(a => a.OccurredAtUtc <= toUtc);

        query = query.OrderByDescending(a => a.OccurredAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<AuditLog>(items, total, page, pageSize);
    }
}
