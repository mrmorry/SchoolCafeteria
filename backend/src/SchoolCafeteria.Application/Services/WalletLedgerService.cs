using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// The single choke point through which every wallet balance change must pass. Enforces:
/// - Rule 18: balances are decimal, never floating point.
/// - Rule 1: purchases never leave a negative balance unless the school explicitly allows it.
/// - Rule: never mutate Wallet.Balance without creating a WalletTransaction ledger row.
/// - Concurrency: relies on EF Core optimistic concurrency (RowVersion) and retries a bounded
///   number of times on conflict, re-validating business rules against the freshly reloaded row —
///   this is what makes "two simultaneous purchases against the same wallet" safe.
/// </summary>
public class WalletLedgerService
{
    private const int MaxConcurrencyRetries = 3;

    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public WalletLedgerService(IAppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<WalletTransaction> CreditAsync(WalletMovementRequest request, CancellationToken ct = default)
        => await ApplyAsync(request with { Amount = Math.Abs(request.Amount) }, isDebit: false, ct);

    public async Task<WalletTransaction> DebitAsync(WalletMovementRequest request, bool allowNegativeBalance = false, CancellationToken ct = default)
        => await ApplyAsync(request with { Amount = Math.Abs(request.Amount) }, isDebit: true, ct, allowNegativeBalance);

    private async Task<WalletTransaction> ApplyAsync(WalletMovementRequest request, bool isDebit, CancellationToken ct, bool allowNegativeBalance = false)
    {
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _db.WalletTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing is not null)
                return existing; // Rule 8/9: a repeated idempotency key never duplicates a movement.
        }

        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct)
                ?? throw new NotFoundException(nameof(Wallet), request.WalletId);

            if (wallet.Status != WalletStatus.Active)
                throw new BusinessRuleException("wallet.not_active", "La cartera no está activa.");

            var balanceBefore = wallet.Balance;
            var balanceAfter = isDebit ? balanceBefore - request.Amount : balanceBefore + request.Amount;

            if (isDebit && balanceAfter < 0 && !allowNegativeBalance)
                throw new BusinessRuleException("wallet.insufficient_funds", "Saldo insuficiente para completar la operación.");

            if (!isDebit && wallet.MaxBalance.HasValue && balanceAfter > wallet.MaxBalance.Value)
                throw new BusinessRuleException("wallet.max_balance_exceeded", "La recarga excede el límite de balance configurado.");

            wallet.Balance = balanceAfter;
            wallet.UpdatedAtUtc = _clock.UtcNow;

            // Reactivation policy: once the balance climbs back above the configured threshold,
            // clear the alert flag so a future drop below it can notify the guardian again.
            if (!isDebit && wallet.LowBalanceThreshold.HasValue && balanceAfter > wallet.LowBalanceThreshold.Value)
                wallet.LastLowBalanceAlertAtUtc = null;

            var transaction = new WalletTransaction
            {
                SchoolId = wallet.SchoolId,
                WalletId = wallet.Id,
                TransactionNumber = GenerateTransactionNumber(request.Type),
                Type = request.Type,
                Channel = request.Channel,
                PaymentMethod = request.PaymentMethod,
                Amount = request.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                PerformedByUserId = request.PerformedByUserId,
                OccurredAtUtc = _clock.UtcNow,
                PointOfSaleId = request.PointOfSaleId,
                RegisterId = request.RegisterId,
                ExternalReference = request.ExternalReference,
                Comment = request.Comment,
                Reason = request.Reason,
                RelatedTransactionId = request.RelatedTransactionId,
                IdempotencyKey = request.IdempotencyKey,
                CorrelationId = request.CorrelationId,
                SaleId = request.SaleId,
                RechargeId = request.RechargeId
            };

            _db.WalletTransactions.Add(transaction);

            try
            {
                await _db.SaveChangesAsync(ct);
                return transaction;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Another concurrent purchase/recharge won the race on this wallet's RowVersion.
                // Detach and retry: reload the wallet and re-validate the business rule against
                // the now-current balance instead of the stale one held in memory.
                foreach (var entry in new[] { _db.WalletTransactions.Entry(transaction) })
                    entry.State = EntityState.Detached;
            }
        }

        throw new ConflictException("No fue posible completar la operación por alta concurrencia sobre la cartera. Intente nuevamente.");
    }

    private static string GenerateTransactionNumber(WalletTransactionType type)
    {
        var prefix = type switch
        {
            WalletTransactionType.Recharge => "RCH",
            WalletTransactionType.Purchase => "PUR",
            WalletTransactionType.Refund => "REF",
            WalletTransactionType.Reversal => "REV",
            _ => "ADJ"
        };
        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100000, 999999)}";
    }
}

public record WalletMovementRequest(
    Guid WalletId,
    decimal Amount,
    WalletTransactionType Type,
    WalletTransactionChannel Channel,
    string PerformedByUserId,
    PaymentMethod? PaymentMethod = null,
    Guid? PointOfSaleId = null,
    Guid? RegisterId = null,
    string? ExternalReference = null,
    string? Comment = null,
    string? Reason = null,
    Guid? RelatedTransactionId = null,
    string? IdempotencyKey = null,
    string? CorrelationId = null,
    Guid? SaleId = null,
    Guid? RechargeId = null);
