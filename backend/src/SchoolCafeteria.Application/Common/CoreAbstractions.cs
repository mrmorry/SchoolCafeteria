namespace SchoolCafeteria.Application.Common;

public interface ICurrentUserService
{
    string? UserId { get; }
    Guid? SchoolId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Permissions { get; }
    IReadOnlyCollection<string> Roles { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool HasPermission(string permissionKey);

    /// <summary>Set only for guardian-portal logins; used to enforce "a guardian only ever sees their own linked students".</summary>
    Guid? GuardianId { get; }

    /// <summary>Set only for student/employee self-service logins.</summary>
    Guid? BuyerId { get; }
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(
        Guid userId, Guid schoolId, string email, IEnumerable<string> roles, IEnumerable<string> permissions,
        Guid? guardianId = null, Guid? buyerId = null);

    string CreateRefreshToken();
    string HashToken(string token);
}

/// <summary>Base for domain-facing exceptions translated to ProblemDetails by the API middleware.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string entity, object key) : base($"{entity} '{key}' no fue encontrado.") { }
}

public sealed class BusinessRuleException : AppException
{
    public string Code { get; }
    public BusinessRuleException(string code, string message) : base(message) => Code = code;
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "No tiene permisos para esta operación.") : base(message) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}
