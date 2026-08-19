using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolCafeteria.Application.Abstractions;
using SchoolCafeteria.Domain.Enums;
using SchoolCafeteria.Infrastructure.Persistence;

namespace SchoolCafeteria.Infrastructure.BackgroundJobs;

/// <summary>
/// Polls the Notification outbox table and delivers pending rows. Stands in for a real message
/// broker (Azure Service Bus / Storage Queues) in this MVP — swapping the polling loop for a
/// queue trigger later does not change NotificationOutboxService's contract. Failed sends are
/// retried up to MaxAttempts times, then dead-lettered; a mail outage never touches the financial
/// operation that queued the notification.
/// </summary>
public class NotificationDispatcherService : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDispatcherService> _logger;

    public NotificationDispatcherService(IServiceProvider serviceProvider, ILogger<NotificationDispatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo inesperado en el despachador de notificaciones.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var pending = await db.Notifications
            .Where(n => n.Status == NotificationStatus.Pending)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        foreach (var notification in pending)
        {
            notification.AttemptCount++;
            notification.LastAttemptAtUtc = DateTime.UtcNow;

            try
            {
                if (notification.Channel == NotificationChannel.Email)
                    await emailSender.SendAsync(notification.Recipient, notification.Subject, notification.Body, ct);

                notification.Status = NotificationStatus.Sent;
                notification.SentAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                notification.LastError = ex.Message;
                notification.Status = notification.AttemptCount >= MaxAttempts ? NotificationStatus.DeadLettered : NotificationStatus.Pending;
                if (notification.Status == NotificationStatus.DeadLettered)
                    _logger.LogWarning("Notificación {Id} enviada a dead-letter tras {Attempts} intentos.", notification.Id, notification.AttemptCount);
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
