using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

/// <summary>
/// The raw UID is never stored or logged in clear text; only a salted hash (for lookup/uniqueness)
/// and a masked value for display are kept.
/// </summary>
public class RfidCredential : SchoolScopedEntity
{
    public Guid BuyerId { get; set; }
    public Buyer? Buyer { get; set; }

    [Sensitive]
    public string CredentialHash { get; set; } = string.Empty;

    public string MaskedValue { get; set; } = string.Empty;
    public RfidCredentialStatus Status { get; set; } = RfidCredentialStatus.Active;
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? BlockedAtUtc { get; set; }
    public string? BlockReason { get; set; }

    public ICollection<RfidAssignmentHistory> History { get; set; } = new List<RfidAssignmentHistory>();
}

public class RfidAssignmentHistory : BaseEntity
{
    public Guid RfidCredentialId { get; set; }
    public RfidCredential? RfidCredential { get; set; }

    public Guid BuyerId { get; set; }
    public string Action { get; set; } = string.Empty; // Issued, Replaced, Blocked, Unblocked, LostReported
    public string? PerformedByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Every read attempt (successful or not) is logged for traceability, without exposing the full UID.</summary>
public class RfidUsageLog : SchoolScopedEntity
{
    public string MaskedValue { get; set; } = string.Empty;
    public bool Matched { get; set; }
    public Guid? BuyerId { get; set; }
    public Guid? PointOfSaleId { get; set; }
    public string Context { get; set; } = string.Empty; // Sale, ManualLookup, Registration
}
