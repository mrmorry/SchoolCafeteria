using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Services;

/// <summary>Administers internal staff login accounts (User rows with a role, not a Buyer) —
/// distinct from Student/Employee/Guardian, which are people the system tracks but that don't
/// necessarily have their own backoffice login.</summary>
public class UserAdminService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public UserAdminService(IAppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<UserSummaryDto>> SearchAsync(Guid schoolId, PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.Users.Where(u => u.SchoolId == schoolId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.FullName)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var dtos = new List<UserSummaryDto>();
        foreach (var user in items) dtos.Add(await MapAsync(user, ct));
        return new PagedResult<UserSummaryDto>(dtos, total, request.Page, request.PageSize);
    }

    public async Task<UserSummaryDto> CreateInternalUserAsync(Guid schoolId, CreateInternalUserRequest request, CancellationToken ct = default)
    {
        var duplicate = await _db.Users.AnyAsync(u => u.SchoolId == schoolId && u.Email == request.Email, ct);
        if (duplicate) throw new BusinessRuleException("user.duplicate_email", $"Ya existe un usuario con el correo '{request.Email}'.");

        if (request.TemporaryPassword.Length < 12)
            throw new BusinessRuleException("user.weak_password", "La contraseña temporal debe tener al menos 12 caracteres.");

        var roles = await _db.Roles.Where(r => request.RoleIds.Contains(r.Id) && r.SchoolId == schoolId).ToListAsync(ct);
        if (roles.Count != request.RoleIds.Distinct().Count())
            throw new BusinessRuleException("user.unknown_role", "Uno o más roles indicados no existen en este colegio.");

        var user = new User
        {
            SchoolId = schoolId, Email = request.Email, FullName = request.FullName,
            PasswordHash = _passwordHasher.Hash(request.TemporaryPassword), IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        foreach (var role in roles)
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await _db.SaveChangesAsync(ct);

        return await MapAsync(user, ct);
    }

    public async Task AssignRoleAsync(Guid schoolId, AssignUserRoleRequest request, CancellationToken ct = default)
    {
        var user = await GetOwnedUserAsync(schoolId, request.UserId, ct);
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId && r.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        var already = await _db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id && ur.PointOfSaleId == request.PointOfSaleId, ct);
        if (already) return;

        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, PointOfSaleId = request.PointOfSaleId });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveRoleAsync(Guid schoolId, Guid userId, Guid userRoleId, CancellationToken ct = default)
    {
        await GetOwnedUserAsync(schoolId, userId, ct);
        var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.Id == userRoleId && ur.UserId == userId, ct)
            ?? throw new NotFoundException(nameof(UserRole), userRoleId);
        _db.UserRoles.Remove(userRole);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(Guid schoolId, Guid userId, SetUserActiveRequest request, CancellationToken ct = default)
    {
        var user = await GetOwnedUserAsync(schoolId, userId, ct);
        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<User> GetOwnedUserAsync(Guid schoolId, Guid userId, CancellationToken ct) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

    private async Task<UserSummaryDto> MapAsync(User user, CancellationToken ct)
    {
        var roles = await (from ur in _db.UserRoles
                            join r in _db.Roles on ur.RoleId equals r.Id
                            where ur.UserId == user.Id
                            select new RoleRefDto(r.Id, r.Name)).ToListAsync(ct);
        return new UserSummaryDto(user.Id, user.Email, user.FullName, user.IsActive, user.MfaEnabled,
            roles, user.EntraObjectId != null, user.LastLoginAtUtc);
    }
}
