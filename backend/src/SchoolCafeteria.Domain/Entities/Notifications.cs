using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class NotificationTemplate : SchoolScopedEntity
{
    public NotificationEvent Event { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Locale { get; set; } = "es";
    public string Subject { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty; // supports {{placeholders}}
    public bool IsActive { get; set; } = true;
}

/// <summary>Database-backed outbox. A worker (IHostedService) polls Pending rows and delivers them,
/// so a mail failure never rolls back the financial operation that queued it.</summary>
public class Notification : SchoolScopedEntity
{
    public NotificationEvent Event { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Dedup key (e.g. "RechargeCompleted:{rechargeId}") to avoid duplicate notifications.</summary>
    public string DeduplicationKey { get; set; } = string.Empty;
}
