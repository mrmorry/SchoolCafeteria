namespace SchoolCafeteria.Application.DTOs;

public record LoginRequest(string Email, string Password, string? MfaCode);
public record EntraIdLoginRequest(string IdToken);
public record LoginResult(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, UserProfileDto User);
public record RefreshRequest(string RefreshToken);
public record UserProfileDto(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
