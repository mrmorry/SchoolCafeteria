using Microsoft.EntityFrameworkCore;
using OtpNet;
using SchoolCafeteria.Application.Abstractions;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// Two coexisting login paths, both landing on the same JWT: local email/password (+ optional TOTP
/// MFA) for any account, and Microsoft Entra ID for staff whose User row is already provisioned
/// and linked (by EntraObjectId, or by email on first Entra sign-in). Either way, authorization
/// downstream of login is identical — permissions always come from this User's own
/// UserRole/RolePermission rows, never from claims embedded in an external token.
/// </summary>
public class AuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEntraIdTokenValidator _entraIdTokenValidator;
    private readonly IDateTimeProvider _clock;

    public AuthService(
        IAppDbContext db, IPasswordHasher passwordHasher, ITokenService tokenService,
        IEntraIdTokenValidator entraIdTokenValidator, IDateTimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _entraIdTokenValidator = entraIdTokenValidator;
        _clock = clock;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r!.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null) await RegisterFailedAttemptAsync(user, ct);
            throw new BusinessRuleException("auth.invalid_credentials", "Correo o contraseña incorrectos.");
        }

        if (user.LockedUntilUtc is not null && user.LockedUntilUtc > _clock.UtcNow)
            throw new BusinessRuleException("auth.locked", $"Cuenta bloqueada temporalmente hasta {user.LockedUntilUtc:u}.");

        if (user.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.MfaCode) || user.MfaSecret is null || !VerifyTotp(user.MfaSecret, request.MfaCode))
                throw new BusinessRuleException("auth.mfa_required", "Código MFA requerido o inválido.");
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;
        user.LastLoginAtUtc = _clock.UtcNow;

        return await IssueTokensAsync(user, ipAddress, ct);
    }

    /// <summary>
    /// Staff-only Entra ID login (see docs/06-runbook.md for scope: Administrador, Finanzas,
    /// Supervisor, Operador, Auditor — tutors/students keep the local flow). The User row must
    /// already exist (created by an administrator via UserAdminService); this never auto-creates
    /// an account, only links one on first successful Entra sign-in. Local password login and MFA
    /// remain fully available afterwards — Entra ID is an additional front door, not a replacement.
    /// </summary>
    public async Task<LoginResult> LoginWithEntraIdAsync(EntraIdLoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var claims = await _entraIdTokenValidator.ValidateAsync(request.IdToken, ct);

        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r!.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.EntraObjectId == claims.ObjectId, ct);

        if (user is null)
        {
            // First Entra sign-in for this person: link by email to a pre-provisioned staff account.
            user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r!.RolePermissions).ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Email == claims.Email, ct);

            if (user is null)
                throw new BusinessRuleException("auth.not_provisioned",
                    "No existe una cuenta de personal asociada a este correo. Solicite a un administrador que la cree primero.");

            user.EntraObjectId = claims.ObjectId;
        }

        if (!user.IsActive)
            throw new BusinessRuleException("auth.inactive", "Esta cuenta está desactivada.");
        if (user.LockedUntilUtc is not null && user.LockedUntilUtc > _clock.UtcNow)
            throw new BusinessRuleException("auth.locked", $"Cuenta bloqueada temporalmente hasta {user.LockedUntilUtc:u}.");

        user.LastLoginAtUtc = _clock.UtcNow;
        return await IssueTokensAsync(user, ipAddress, ct);
    }

    private async Task<LoginResult> IssueTokensAsync(User user, string? ipAddress, CancellationToken ct)
    {
        var roles = user.UserRoles.Select(ur => ur.Role!.Name).Distinct().ToList();
        var permissions = user.UserRoles.SelectMany(ur => ur.Role!.RolePermissions.Select(rp => rp.Permission!.Key)).Distinct().ToList();

        var (accessToken, expiresAtUtc) = _tokenService.CreateAccessToken(user.Id, user.SchoolId, user.Email, roles, permissions, user.GuardianId, user.BuyerId);
        var refreshToken = _tokenService.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = _tokenService.HashToken(refreshToken),
            ExpiresAtUtc = _clock.UtcNow.Add(RefreshTokenLifetime), CreatedByIp = ipAddress
        });
        await _db.SaveChangesAsync(ct);

        return new LoginResult(accessToken, expiresAtUtc, refreshToken,
            new UserProfileDto(user.Id, user.Email, user.FullName, roles, permissions));
    }

    public async Task<LoginResult> RefreshAsync(RefreshRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var hash = _tokenService.HashToken(request.RefreshToken);
        var token = await _db.RefreshTokens.Include(t => t.User).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r!.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.RevokedAtUtc is not null || token.ExpiresAtUtc < _clock.UtcNow)
            throw new BusinessRuleException("auth.invalid_refresh_token", "Refresh token inválido o expirado.");

        // Rotation: revoke the used token and mint a new one, never reuse the same refresh token twice.
        token.RevokedAtUtc = _clock.UtcNow;
        var newRefreshToken = _tokenService.CreateRefreshToken();
        token.ReplacedByTokenHash = _tokenService.HashToken(newRefreshToken);

        var user = token.User!;
        var roles = user.UserRoles.Select(ur => ur.Role!.Name).Distinct().ToList();
        var permissions = user.UserRoles.SelectMany(ur => ur.Role!.RolePermissions.Select(rp => rp.Permission!.Key)).Distinct().ToList();
        var (accessToken, expiresAtUtc) = _tokenService.CreateAccessToken(user.Id, user.SchoolId, user.Email, roles, permissions, user.GuardianId, user.BuyerId);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = token.ReplacedByTokenHash,
            ExpiresAtUtc = _clock.UtcNow.Add(RefreshTokenLifetime), CreatedByIp = ipAddress
        });
        await _db.SaveChangesAsync(ct);

        return new LoginResult(accessToken, expiresAtUtc, newRefreshToken,
            new UserProfileDto(user.Id, user.Email, user.FullName, roles, permissions));
    }

    public async Task RevokeAllSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var t in tokens) t.RevokedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task RegisterFailedAttemptAsync(User user, CancellationToken ct)
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= MaxFailedAttempts)
            user.LockedUntilUtc = _clock.UtcNow.Add(LockoutDuration);
        await _db.SaveChangesAsync(ct);
    }

    private static bool VerifyTotp(string base32Secret, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
    }
}
