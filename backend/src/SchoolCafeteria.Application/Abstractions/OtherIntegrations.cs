namespace SchoolCafeteria.Application.Abstractions;

/// <summary>
/// Email sending abstraction. Swap SmtpEmailSender for an Azure Communication Services / SendGrid
/// adapter purely via DI — Application code never depends on a specific provider SDK.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>
/// Blob/file storage abstraction (import templates, result files, product images, exported reports).
/// The default implementation writes to a local, non-container-persistent path in dev; the Azure
/// adapter targets Blob Storage in production.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string containerName, string fileName, CancellationToken ct = default);
    string GetPublicOrSignedUrl(string containerName, string fileName);
}

/// <summary>
/// Represents a school's external Student Information System. No concrete SIS is targeted in v1 —
/// CsvStudentSourceAdapter (manual upload) is the only implementation shipped; an ApiStudentSourceAdapter
/// can be added later without touching the import domain logic.
/// </summary>
public interface IStudentSourceAdapter
{
    string SourceName { get; }
    Task<IReadOnlyList<ExternalStudentRecord>> FetchAsync(CancellationToken ct = default);
}

public record ExternalStudentRecord(
    string StudentCode,
    string FirstName,
    string LastName,
    string? Level,
    string? Section,
    string? StudentEmail,
    string GuardianFullName,
    string GuardianEmail,
    string? GuardianPhone,
    bool Active);

/// <summary>
/// Validates an ID token issued by an external identity provider (Microsoft Entra ID) and returns
/// the normalized claims AuthService needs to resolve or link a local staff User. Kept separate
/// from ITokenService (which issues *our own* JWTs) — this interface only ever consumes tokens
/// minted elsewhere. No concrete provider is wired against a real tenant in this build (see
/// docs/06-runbook.md); EntraIdTokenValidator implements this against the standard Entra ID OIDC
/// discovery document once TenantId/ClientId are configured.
/// </summary>
public interface IEntraIdTokenValidator
{
    /// <summary>True once TenantId/ClientId/Instance are all configured — lets callers fail with a
    /// clear "not configured" business error instead of an obscure network/validation failure.</summary>
    bool IsConfigured { get; }

    Task<ExternalIdentityClaims> ValidateAsync(string idToken, CancellationToken ct = default);
}

public record ExternalIdentityClaims(string ObjectId, string Email, string DisplayName);

/// <summary>
/// Decouples the API from any specific RFID reader hardware. The default operating mode is
/// "keyboard wedge" (the reader types the UID into a focused input on the client) which needs no
/// server-side contract beyond receiving the UID string. This interface exists for future
/// integrations (WebUSB/WebSerial bridge, vendor SDK agent) that push reads server-side.
/// </summary>
public interface IRfidReaderProvider
{
    string ProviderName { get; }
    Task<string?> ReadNextUidAsync(CancellationToken ct = default);
}
