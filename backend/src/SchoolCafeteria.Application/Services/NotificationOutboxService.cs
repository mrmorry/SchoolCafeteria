using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// Queues notifications into a database outbox instead of sending synchronously. A background
/// worker (see Infrastructure.BackgroundJobs.NotificationDispatcherService) delivers them with
/// retry + dead-letter, so a mail provider outage never rolls back a completed financial
/// operation (rule: "los correos fallidos no deben revertir operaciones financieras completadas").
/// </summary>
public class NotificationOutboxService
{
    private readonly IAppDbContext _db;

    public NotificationOutboxService(IAppDbContext db) => _db = db;

    public async Task EnqueueAsync(
        Guid schoolId, NotificationEvent evt, NotificationChannel channel, string recipient,
        string subject, string body, string correlationId, string deduplicationKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipient))
            return;

        var alreadyQueued = await _db.Notifications
            .AnyAsync(n => n.DeduplicationKey == deduplicationKey, ct);
        if (alreadyQueued)
            return; // avoids duplicate notifications for the same business event

        _db.Notifications.Add(new Notification
        {
            SchoolId = schoolId,
            Event = evt,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Pending,
            CorrelationId = correlationId,
            DeduplicationKey = deduplicationKey
        });
        await _db.SaveChangesAsync(ct);
    }
}
