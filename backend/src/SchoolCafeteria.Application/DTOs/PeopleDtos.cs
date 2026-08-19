using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.DTOs;

public record StudentDto(
    Guid Id, string StudentCode, string FirstName, string LastName, StudentStatus Status,
    Guid? SchoolLevelId, string? SchoolLevelName, Guid? SchoolSectionId, string? SchoolSectionName,
    string? StudentEmail, Guid BuyerId, Guid WalletId, decimal WalletBalance, bool HasRfid,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public record CreateStudentRequest(
    string StudentCode, string FirstName, string LastName, Guid? SchoolLevelId, Guid? SchoolSectionId,
    string? StudentEmail, string GuardianFullName, string GuardianEmail, string? GuardianPhone);

public record UpdateStudentRequest(
    string FirstName, string LastName, StudentStatus Status, Guid? SchoolLevelId, Guid? SchoolSectionId,
    string? StudentEmail);

public record GuardianDto(Guid Id, string FullName, string Email, string? Phone, IReadOnlyList<GuardianStudentLinkDto> Students);
public record GuardianStudentLinkDto(Guid StudentId, string StudentFullName, string Relationship, bool IsPrimary,
    bool CanRecharge, bool CanViewHistory, bool CanManageRfid, bool CanConfigureAlerts);

public record CreateGuardianRequest(string FullName, string Email, string? Phone);
public record LinkGuardianStudentRequest(Guid GuardianId, Guid StudentId, string Relationship, bool IsPrimary,
    bool CanRecharge, bool CanViewHistory, bool CanManageRfid, bool CanConfigureAlerts);

public record EmployeeDto(Guid Id, string EmployeeCode, string FullName, string Email, string EmployeeType,
    EmployeeStatus Status, Guid BuyerId, Guid WalletId, decimal WalletBalance, bool HasRfid);

public record CreateEmployeeRequest(string EmployeeCode, string FullName, string Email, string EmployeeType);

public record BuyerSummaryDto(Guid BuyerId, BuyerType Type, string FullName, bool IsActive,
    Guid WalletId, decimal WalletBalance, WalletStatus WalletStatus, string? RfidMaskedValue);
