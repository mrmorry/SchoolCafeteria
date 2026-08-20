using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

public class WalletService
{
    private readonly IAppDbContext _db;
    private readonly WalletLedgerService _ledger;
    private readonly NotificationOutboxService _notifications;

    public WalletService(IAppDbContext db, WalletLedgerService ledger, NotificationOutboxService notifications)
    {
        _db = db;
        _ledger = ledger;
        _notifications = notifications;
    }

    public async Task<WalletDto?> GetByBuyerIdAsync(Guid buyerId, CancellationToken ct = default)
    {
        var wallet = await _db.Wallets.Include(w => w.Buyer).FirstOrDefaultAsync(w => w.BuyerId == buyerId, ct);
        return wallet is null ? null : ToDto(wallet);
    }

    public async Task<PagedResult<WalletTransactionDto>> GetTransactionsAsync(Guid walletId, PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.WalletTransactions.Where(t => t.WalletId == walletId).OrderByDescending(t => t.OccurredAtUtc);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new PagedResult<WalletTransactionDto>(items.Select(ToDto).ToList(), total, request.Page, request.PageSize);
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> GetLastPurchasesAsync(Guid walletId, int count, CancellationToken ct = default)
    {
        var items = await _db.WalletTransactions
            .Where(t => t.WalletId == walletId && t.Type == WalletTransactionType.Purchase)
            .OrderByDescending(t => t.OccurredAtUtc).Take(count).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task SetLowBalanceThresholdAsync(Guid walletId, SetLowBalanceThresholdRequest request, CancellationToken ct = default)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == walletId, ct)
            ?? throw new NotFoundException(nameof(Wallet), walletId);
        wallet.LowBalanceThreshold = request.Threshold;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Financial adjustment requiring an explicit reason (rule 14/15). Authorization is enforced at the API layer via permissions.</summary>
    public async Task<WalletTransactionDto> ManualAdjustmentAsync(Guid schoolId, ManualAdjustmentRequest request, string performedByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("wallet.reason_required", "Todo ajuste manual requiere un motivo.");

        var movement = request.IsPositive
            ? await _ledger.CreditAsync(new WalletMovementRequest(
                request.WalletId, request.Amount, WalletTransactionType.AdjustmentPositive,
                WalletTransactionChannel.AdminAdjustment, performedByUserId, Reason: request.Reason), ct)
            : await _ledger.DebitAsync(new WalletMovementRequest(
                request.WalletId, request.Amount, WalletTransactionType.AdjustmentNegative,
                WalletTransactionChannel.AdminAdjustment, performedByUserId, Reason: request.Reason), ct: ct);

        return ToDto(movement);
    }

    /// <summary>
    /// Fires only on the downward crossing of the threshold and is throttled so a guardian isn't
    /// emailed on every balance check — see rule: "evitar correos repetitivos en cada consulta".
    /// </summary>
    public async Task CheckAndQueueLowBalanceAlertAsync(Guid walletId, CancellationToken ct = default)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == walletId, ct);
        if (wallet?.LowBalanceThreshold is null || wallet.Balance > wallet.LowBalanceThreshold) return;
        if (wallet.LastLowBalanceAlertAtUtc is not null) return; // already alerted since the last recharge/reactivation

        var student = await _db.Students.FirstOrDefaultAsync(s => s.BuyerId == wallet.BuyerId, ct);
        if (student is null) return;
        var link = await _db.GuardianStudents.Where(gs => gs.StudentId == student.Id && gs.IsPrimary).FirstOrDefaultAsync(ct);
        if (link is null) return;
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == link.GuardianId, ct);
        if (guardian is null) return;

        await _notifications.EnqueueAsync(wallet.SchoolId, NotificationEvent.LowBalance, NotificationChannel.Email, guardian.Email,
            "Alerta de balance bajo", $"El balance de {student.FullName} es {wallet.Balance:0.00}, por debajo del umbral configurado.",
            Guid.NewGuid().ToString(), $"LowBalance:{wallet.Id}:{DateTime.UtcNow:yyyyMMdd}", ct);

        wallet.LastLowBalanceAlertAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static WalletDto ToDto(Wallet w) => new(w.Id, w.BuyerId, w.Buyer?.FullName ?? string.Empty, w.Currency,
        w.Balance, w.HeldBalance, w.Status, w.MaxBalance, w.LowBalanceThreshold);

    private static WalletTransactionDto ToDto(WalletTransaction t) => new(t.Id, t.TransactionNumber, t.Type, t.Status,
        t.Channel, t.PaymentMethod, t.Amount, t.BalanceBefore, t.BalanceAfter, t.PerformedByUserId, t.OccurredAtUtc,
        t.Comment, t.Reason, t.ExternalReference);
}
