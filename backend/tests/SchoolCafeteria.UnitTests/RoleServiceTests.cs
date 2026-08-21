using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.UnitTests.TestSupport;
using Xunit;

namespace SchoolCafeteria.UnitTests;

public class RoleServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly Guid _schoolId = Guid.NewGuid();

    public RoleServiceTests()
    {
        using var db = _factory.CreateContext();
        db.Permissions.AddRange(
            new Permission { Key = "pos.sell", Module = "Pos", Description = "Vender" },
            new Permission { Key = "pos.refund", Module = "Pos", Description = "Anular" },
            new Permission { Key = "reports.read", Module = "Reports", Description = "Ver reportes" });
        db.SaveChanges();
    }

    [Fact]
    public async Task SetPermissions_ReplacesFullSet_AddingAndRemoving()
    {
        using var db = _factory.CreateContext();
        var sut = new RoleService(db);
        var role = await sut.CreateRoleAsync(_schoolId, new CreateRoleRequest("Cajero Junior", "Rol de prueba"));

        await sut.SetPermissionsAsync(_schoolId, role.Id, new SetRolePermissionsRequest(new[] { "pos.sell", "reports.read" }));
        var afterFirstSet = (await sut.GetRolesAsync(_schoolId)).Single(r => r.Id == role.Id);
        Assert.Equal(new[] { "pos.sell", "reports.read" }, afterFirstSet.Permissions.OrderBy(p => p).ToArray());

        // Second call removes reports.read and adds pos.refund — a full replace, not an additive merge.
        await sut.SetPermissionsAsync(_schoolId, role.Id, new SetRolePermissionsRequest(new[] { "pos.sell", "pos.refund" }));
        var afterSecondSet = (await sut.GetRolesAsync(_schoolId)).Single(r => r.Id == role.Id);
        Assert.Equal(new[] { "pos.refund", "pos.sell" }, afterSecondSet.Permissions.OrderBy(p => p).ToArray());
    }

    [Fact]
    public async Task SetPermissions_UnknownKey_Throws()
    {
        using var db = _factory.CreateContext();
        var sut = new RoleService(db);
        var role = await sut.CreateRoleAsync(_schoolId, new CreateRoleRequest("Rol X", null));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sut.SetPermissionsAsync(_schoolId, role.Id, new SetRolePermissionsRequest(new[] { "no.existe" })));
    }

    [Fact]
    public async Task DeleteRole_WithAssignedUsers_Throws()
    {
        using var db = _factory.CreateContext();
        var sut = new RoleService(db);
        var role = await sut.CreateRoleAsync(_schoolId, new CreateRoleRequest("Rol con usuarios", null));

        var user = new User { SchoolId = _schoolId, Email = "u@demo.local", FullName = "U", PasswordHash = "x", IsActive = true };
        db.Users.Add(user);
        db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.SaveChanges();

        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.DeleteRoleAsync(_schoolId, role.Id));
    }

    [Fact]
    public async Task DeleteRole_SystemRole_Throws()
    {
        using var db = _factory.CreateContext();
        var systemRole = new Role { SchoolId = _schoolId, Name = "Administrador", IsSystemRole = true };
        db.Roles.Add(systemRole);
        db.SaveChanges();

        var sut = new RoleService(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.DeleteRoleAsync(_schoolId, systemRole.Id));
    }

    public void Dispose() => _factory.Dispose();
}
