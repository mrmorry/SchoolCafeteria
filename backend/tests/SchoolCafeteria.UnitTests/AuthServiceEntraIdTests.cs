using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using SchoolCafeteria.Application.Abstractions;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Infrastructure.Services;
using SchoolCafeteria.UnitTests.TestSupport;
using Xunit;

namespace SchoolCafeteria.UnitTests;

public class AuthServiceEntraIdTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly FakeEntraIdTokenValidator _entraValidator = new();

    private AuthService CreateSut(SchoolCafeteria.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "SchoolCafeteria.Tests",
            ["Jwt:Audience"] = "SchoolCafeteria.Tests.Clients",
            ["Jwt:SigningKey"] = "unit-test-signing-key-not-a-real-secret-32chars",
            ["Jwt:AccessTokenMinutes"] = "15"
        }).Build();

        return new AuthService(db, new PasswordHasher(), new TokenService(config), _entraValidator, new FixedDateTimeProvider());
    }

    private Guid SeedStaffUser(string email)
    {
        using var db = _factory.CreateContext();
        var role = new Role { SchoolId = _schoolId, Name = "Operador", IsSystemRole = true };
        db.Roles.Add(role);
        var user = new User { SchoolId = _schoolId, Email = email, FullName = "Operador de Prueba", PasswordHash = "unused", IsActive = true };
        db.Users.Add(user);
        db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.SaveChanges();
        return user.Id;
    }

    [Fact]
    public async Task LoginWithEntraId_NoMatchingAccount_ThrowsNotProvisioned()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);
        _entraValidator.NextClaims = new ExternalIdentityClaims("oid-123", "nadie@demo.local", "Nadie");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sut.LoginWithEntraIdAsync(new EntraIdLoginRequest("fake-id-token"), "127.0.0.1"));
        Assert.Equal("auth.not_provisioned", ex.Code);
    }

    [Fact]
    public async Task LoginWithEntraId_FirstSignIn_LinksByEmailAndIssuesToken()
    {
        var userId = SeedStaffUser("operador@demo.local");
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);
        _entraValidator.NextClaims = new ExternalIdentityClaims("oid-abc", "operador@demo.local", "Operador de Prueba");

        var result = await sut.LoginWithEntraIdAsync(new EntraIdLoginRequest("fake-id-token"), "127.0.0.1");

        Assert.Equal(userId, result.User.Id);
        Assert.Contains("Operador", result.User.Roles);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));

        var linkedUser = db.Users.Single(u => u.Id == userId);
        Assert.Equal("oid-abc", linkedUser.EntraObjectId);
    }

    [Fact]
    public async Task LoginWithEntraId_SubsequentSignIn_MatchesByObjectIdEvenIfEmailChanged()
    {
        var userId = SeedStaffUser("operador2@demo.local");
        using (var db = _factory.CreateContext())
        {
            var user = db.Users.Single(u => u.Id == userId);
            user.EntraObjectId = "oid-already-linked";
            db.SaveChanges();
        }

        using var db2 = _factory.CreateContext();
        var sut = CreateSut(db2);
        // Email on the token differs from what's stored locally (e.g. renamed in Entra) — the
        // object id match still resolves the same account.
        _entraValidator.NextClaims = new ExternalIdentityClaims("oid-already-linked", "nuevo-correo@demo.local", "Operador de Prueba");

        var result = await sut.LoginWithEntraIdAsync(new EntraIdLoginRequest("fake-id-token"), "127.0.0.1");
        Assert.Equal(userId, result.User.Id);
    }

    [Fact]
    public async Task LoginWithEntraId_InactiveAccount_Throws()
    {
        var userId = SeedStaffUser("inactivo@demo.local");
        using (var db = _factory.CreateContext())
        {
            var user = db.Users.Single(u => u.Id == userId);
            user.IsActive = false;
            db.SaveChanges();
        }

        using var db2 = _factory.CreateContext();
        var sut = CreateSut(db2);
        _entraValidator.NextClaims = new ExternalIdentityClaims("oid-xyz", "inactivo@demo.local", "Inactivo");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sut.LoginWithEntraIdAsync(new EntraIdLoginRequest("fake-id-token"), "127.0.0.1"));
        Assert.Equal("auth.inactive", ex.Code);
    }

    public void Dispose() => _factory.Dispose();
}
