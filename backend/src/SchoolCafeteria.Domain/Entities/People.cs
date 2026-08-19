using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class SchoolLevel : SchoolScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<SchoolSection> Sections { get; set; } = new List<SchoolSection>();
}

public class SchoolSection : SchoolScopedEntity
{
    public Guid SchoolLevelId { get; set; }
    public SchoolLevel? SchoolLevel { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Base "purchaser" concept shared by Student and Employee (Table-Per-Hierarchy).</summary>
public class Buyer : SchoolScopedEntity
{
    public BuyerType Type { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Wallet? Wallet { get; set; }
    public ICollection<RfidCredential> RfidCredentials { get; set; } = new List<RfidCredential>();
}

public class Student : SoftDeletableSchoolEntity
{
    public Guid BuyerId { get; set; }
    public Buyer? Buyer { get; set; }

    public string StudentCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();

    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public Guid? SchoolLevelId { get; set; }
    public SchoolLevel? SchoolLevelRef { get; set; }
    public Guid? SchoolSectionId { get; set; }
    public SchoolSection? SchoolSectionRef { get; set; }

    public string? StudentEmail { get; set; }

    public ICollection<GuardianStudent> GuardianLinks { get; set; } = new List<GuardianStudent>();
}

public class Employee : SoftDeletableSchoolEntity
{
    public Guid BuyerId { get; set; }
    public Buyer? Buyer { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmployeeType { get; set; } = "Teacher"; // Teacher | Administrative | Other
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
}

public class Guardian : SoftDeletableSchoolEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public ICollection<GuardianStudent> StudentLinks { get; set; } = new List<GuardianStudent>();
}

/// <summary>Bridge table: a guardian can be primary or secondary, with per-row configurable permissions.</summary>
public class GuardianStudent : BaseEntity
{
    public Guid GuardianId { get; set; }
    public Guardian? Guardian { get; set; }
    public Guid StudentId { get; set; }
    public Student? Student { get; set; }

    public string Relationship { get; set; } = "Parent";
    public bool IsPrimary { get; set; }
    public bool CanRecharge { get; set; } = true;
    public bool CanViewHistory { get; set; } = true;
    public bool CanManageRfid { get; set; } = true;
    public bool CanConfigureAlerts { get; set; } = true;
}
