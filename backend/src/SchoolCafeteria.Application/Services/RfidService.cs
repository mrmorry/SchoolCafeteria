using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// The raw UID never touches logs or the database in clear text — only a SHA-256 hash (salted
/// with the school id) for lookups and a masked value ("****1234") for display.
/// </summary>
public class RfidService
{
    private readonly IAppDbContext _db;
    private readonly NotificationOutboxService _notifications;
    private readonly IDateTimeProvider _clock;

    public RfidService(IAppDbContext db, NotificationOutboxService notifications, IDateTimeProvider clock)
    {
        _db = db;
        _notifications = notifications;
        _clock = clock;
    }

    public static string Hash(string rawUid, Guid schoolId)
    {
        var bytes = Encoding.UTF8.GetBytes($"{schoolId}:{rawUid}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string Mask(string rawUid) => rawUid.Length <= 4 ? "****" : $"****{rawUid[^4..]}";

    public async Task<RfidCredentialDto> IssueAsync(Guid schoolId, IssueRfidRequest request, string performedByUserId, CancellationToken ct = default)
    {
        var hash = Hash(request.RawUid, schoolId);
        var alreadyActive = await _db.RfidCredentials.AnyAsync(c => c.CredentialHash == hash && c.Status == RfidCredentialStatus.Active, ct);
        if (alreadyActive)
            throw new BusinessRuleException("rfid.already_assigned", "Esta credencial ya está activa y asociada a otro comprador.");

        var buyer = await _db.Buyers.FirstOrDefaultAsync(b => b.Id == request.BuyerId, ct)
            ?? throw new NotFoundException(nameof(Buyer), request.BuyerId);

        var credential = new RfidCredential
        {
            SchoolId = schoolId,
            BuyerId = buyer.Id,
            CredentialHash = hash,
            MaskedValue = Mask(request.RawUid),
            Status = RfidCredentialStatus.Active,
            IssuedAtUtc = _clock.UtcNow
        };
        _db.RfidCredentials.Add(credential);
        _db.RfidAssignmentHistories.Add(new RfidAssignmentHistory
        {
            RfidCredentialId = credential.Id, BuyerId = buyer.Id, Action = "Issued", PerformedByUserId = performedByUserId
        });
        await _db.SaveChangesAsync(ct);
        return ToDto(credential, buyer.FullName);
    }

    public async Task<RfidCredentialDto> ReplaceAsync(Guid schoolId, ReplaceRfidRequest request, string performedByUserId, CancellationToken ct = default)
    {
        var current = await _db.RfidCredentials
            .Where(c => c.BuyerId == request.BuyerId && c.Status == RfidCredentialStatus.Active)
            .FirstOrDefaultAsync(ct);
        if (current is not null)
        {
            current.Status = RfidCredentialStatus.Replaced;
            _db.RfidAssignmentHistories.Add(new RfidAssignmentHistory
            {
                RfidCredentialId = current.Id, BuyerId = current.BuyerId, Action = "Replaced",
                PerformedByUserId = performedByUserId, Reason = request.Reason
            });
        }

        var issued = await IssueAsync(schoolId, new IssueRfidRequest(request.BuyerId, request.NewRawUid), performedByUserId, ct);
        await NotifyAsync(schoolId, request.BuyerId, NotificationEvent.RfidReplaced, "Credencial RFID reemplazada", ct);
        return issued;
    }

    public async Task BlockAsync(Guid schoolId, BlockRfidRequest request, string performedByUserId, CancellationToken ct = default)
    {
        var credential = await _db.RfidCredentials.FirstOrDefaultAsync(c => c.Id == request.RfidCredentialId, ct)
            ?? throw new NotFoundException(nameof(RfidCredential), request.RfidCredentialId);

        credential.Status = RfidCredentialStatus.Blocked;
        credential.BlockedAtUtc = _clock.UtcNow;
        credential.BlockReason = request.Reason;
        _db.RfidAssignmentHistories.Add(new RfidAssignmentHistory
        {
            RfidCredentialId = credential.Id, BuyerId = credential.BuyerId, Action = "Blocked",
            PerformedByUserId = performedByUserId, Reason = request.Reason
        });
        await _db.SaveChangesAsync(ct);
        await NotifyAsync(schoolId, credential.BuyerId, NotificationEvent.RfidBlocked, "Credencial RFID bloqueada", ct);
    }

    public async Task UnblockAsync(Guid schoolId, UnblockRfidRequest request, string performedByUserId, CancellationToken ct = default)
    {
        var credential = await _db.RfidCredentials.FirstOrDefaultAsync(c => c.Id == request.RfidCredentialId, ct)
            ?? throw new NotFoundException(nameof(RfidCredential), request.RfidCredentialId);

        credential.Status = RfidCredentialStatus.Active;
        credential.BlockedAtUtc = null;
        credential.BlockReason = null;
        _db.RfidAssignmentHistories.Add(new RfidAssignmentHistory
        {
            RfidCredentialId = credential.Id, BuyerId = credential.BuyerId, Action = "Unblocked",
            PerformedByUserId = performedByUserId, Reason = request.Reason
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReportLostAsync(Guid schoolId, ReportLostRfidRequest request, string performedByUserId, CancellationToken ct = default)
    {
        var credential = await _db.RfidCredentials.FirstOrDefaultAsync(c => c.Id == request.RfidCredentialId, ct)
            ?? throw new NotFoundException(nameof(RfidCredential), request.RfidCredentialId);

        credential.Status = RfidCredentialStatus.Lost;
        credential.BlockedAtUtc = _clock.UtcNow;
        credential.BlockReason = request.Reason;
        _db.RfidAssignmentHistories.Add(new RfidAssignmentHistory
        {
            RfidCredentialId = credential.Id, BuyerId = credential.BuyerId, Action = "LostReported",
            PerformedByUserId = performedByUserId, Reason = request.Reason
        });
        await _db.SaveChangesAsync(ct);
        await NotifyAsync(schoolId, credential.BuyerId, NotificationEvent.RfidBlocked, "Credencial RFID reportada como perdida y bloqueada", ct);
    }

    /// <summary>Used by the POS: swipe/scan lookup. Every attempt is logged (matched or not), UID never stored raw.</summary>
    public async Task<RfidLookupResult?> LookupAsync(Guid schoolId, string rawUid, Guid? pointOfSaleId, CancellationToken ct = default)
    {
        var hash = Hash(rawUid, schoolId);
        var credential = await _db.RfidCredentials
            .FirstOrDefaultAsync(c => c.CredentialHash == hash && c.Status == RfidCredentialStatus.Active, ct);

        _db.RfidUsageLogs.Add(new RfidUsageLog
        {
            SchoolId = schoolId, MaskedValue = Mask(rawUid), Matched = credential is not null,
            BuyerId = credential?.BuyerId, PointOfSaleId = pointOfSaleId, Context = "Sale"
        });
        await _db.SaveChangesAsync(ct);

        if (credential is null) return null;

        var buyer = await _db.Buyers.FirstAsync(b => b.Id == credential.BuyerId, ct);
        var wallet = await _db.Wallets.FirstAsync(w => w.BuyerId == credential.BuyerId, ct);

        return new RfidLookupResult(buyer.Id, buyer.FullName, buyer.Type.ToString(), wallet.Id, wallet.Balance,
            wallet.Status.ToString(), credential.MaskedValue, wallet.Status == WalletStatus.Active && buyer.IsActive, null);
    }

    private async Task NotifyAsync(Guid schoolId, Guid buyerId, NotificationEvent evt, string subject, CancellationToken ct)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.BuyerId == buyerId, ct);
        string? recipient = null;
        if (student is not null)
        {
            var link = await _db.GuardianStudents.Where(gs => gs.StudentId == student.Id && gs.IsPrimary).FirstOrDefaultAsync(ct);
            if (link is not null)
                recipient = (await _db.Guardians.FirstOrDefaultAsync(g => g.Id == link.GuardianId, ct))?.Email;
        }
        if (recipient is null) return;

        await _notifications.EnqueueAsync(schoolId, evt, NotificationChannel.Email, recipient, subject,
            $"{subject} el {_clock.UtcNow:yyyy-MM-dd HH:mm} UTC.", Guid.NewGuid().ToString(),
            $"{evt}:{buyerId}:{_clock.UtcNow:yyyyMMddHHmmss}", ct);
    }

    private static RfidCredentialDto ToDto(RfidCredential c, string buyerName) =>
        new(c.Id, c.BuyerId, buyerName, c.MaskedValue, c.Status.ToString(), c.IssuedAtUtc, c.BlockedAtUtc, c.BlockReason);
}
