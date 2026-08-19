using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Abstractions;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

public class RechargeService
{
    private readonly IAppDbContext _db;
    private readonly WalletLedgerService _ledger;
    private readonly IPaymentGateway _paymentGateway;
    private readonly NotificationOutboxService _notifications;
    private readonly IDateTimeProvider _clock;

    public RechargeService(
        IAppDbContext db, WalletLedgerService ledger, IPaymentGateway paymentGateway,
        NotificationOutboxService notifications, IDateTimeProvider clock)
    {
        _db = db;
        _ledger = ledger;
        _paymentGateway = paymentGateway;
        _notifications = notifications;
        _clock = clock;
    }

    /// <summary>Channel: PointOfSale or CashierOffice. Completes synchronously — cash/card at the counter is already confirmed.</summary>
    public async Task<RechargeDto> RechargePresentialAsync(
        Guid schoolId, RechargePresentialRequest request, string performedByUserId,
        WalletTransactionChannel channel, Guid? pointOfSaleId, Guid? registerId, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new BusinessRuleException("recharge.invalid_amount", "El monto de la recarga debe ser mayor a cero.");

        var existing = await _db.Recharges.FirstOrDefaultAsync(r => r.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null)
            return await ToDtoAsync(existing, ct);

        var wallet = await _db.Wallets.Include(w => w.Buyer).FirstOrDefaultAsync(w => w.Id == request.WalletId, ct)
            ?? throw new NotFoundException(nameof(Wallet), request.WalletId);

        var recharge = new Recharge
        {
            SchoolId = schoolId,
            WalletId = wallet.Id,
            Amount = request.Amount,
            Currency = wallet.Currency,
            Status = RechargeStatus.Processing,
            Channel = channel,
            PaymentMethod = request.PaymentMethod,
            IdempotencyKey = request.IdempotencyKey,
            RequestedByUserId = performedByUserId
        };
        _db.Recharges.Add(recharge);
        await _db.SaveChangesAsync(ct);

        var movement = await _ledger.CreditAsync(new WalletMovementRequest(
            WalletId: wallet.Id, Amount: request.Amount, Type: WalletTransactionType.Recharge,
            Channel: channel, PerformedByUserId: performedByUserId, PaymentMethod: request.PaymentMethod,
            PointOfSaleId: pointOfSaleId, RegisterId: registerId, Comment: request.Comment,
            IdempotencyKey: $"recharge:{recharge.Id}", RechargeId: recharge.Id), ct);

        recharge.Status = RechargeStatus.Completed;
        recharge.WalletTransactionId = movement.Id;
        recharge.CompletedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        await QueueRechargeNotificationAsync(recharge, wallet, movement, ct);
        return await ToDtoAsync(recharge, ct);
    }

    /// <summary>Channel: GuardianPortal/StudentPortal/Api. Creates a pending order; completion happens via webhook.</summary>
    public async Task<(RechargeDto Recharge, string CheckoutUrl)> RechargeDigitalAsync(
        Guid schoolId, RechargeDigitalRequest request, string performedByUserId, WalletTransactionChannel channel, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new BusinessRuleException("recharge.invalid_amount", "El monto de la recarga debe ser mayor a cero.");

        var existing = await _db.Recharges.FirstOrDefaultAsync(r => r.IdempotencyKey == request.IdempotencyKey, ct);
        if (existing is not null)
        {
            var order = existing.PaymentOrderId is null ? null
                : await _db.PaymentOrders.FirstOrDefaultAsync(o => o.Id == existing.PaymentOrderId, ct);
            return (await ToDtoAsync(existing, ct), order?.ProviderCheckoutReference ?? string.Empty);
        }

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct)
            ?? throw new NotFoundException(nameof(Wallet), request.WalletId);

        var paymentOrder = new PaymentOrder
        {
            SchoolId = schoolId,
            Provider = _paymentGateway.ProviderName,
            Amount = request.Amount,
            Currency = wallet.Currency,
            Status = PaymentOrderStatus.Pending
        };
        _db.PaymentOrders.Add(paymentOrder);
        await _db.SaveChangesAsync(ct);

        var gatewayResult = await _paymentGateway.CreateOrderAsync(
            new CreatePaymentOrderRequest(paymentOrder.Id, request.Amount, wallet.Currency, "Recarga de cartera escolar", request.ReturnUrl), ct);

        paymentOrder.ProviderOrderId = gatewayResult.ProviderOrderId;
        paymentOrder.ProviderCheckoutReference = gatewayResult.CheckoutUrl;
        paymentOrder.ExpiresAtUtc = gatewayResult.ExpiresAtUtc;

        var recharge = new Recharge
        {
            SchoolId = schoolId,
            WalletId = wallet.Id,
            Amount = request.Amount,
            Currency = wallet.Currency,
            Status = RechargeStatus.Pending,
            Channel = channel,
            PaymentMethod = PaymentMethod.OnlinePayment,
            IdempotencyKey = request.IdempotencyKey,
            RequestedByUserId = performedByUserId,
            PaymentOrderId = paymentOrder.Id
        };
        _db.Recharges.Add(recharge);
        await _db.SaveChangesAsync(ct);

        return (await ToDtoAsync(recharge, ct), gatewayResult.CheckoutUrl);
    }

    /// <summary>
    /// Idempotent webhook handler: unique (Provider, ExternalEventId) prevents a duplicated
    /// webhook delivery from completing the same recharge twice.
    /// </summary>
    public async Task HandlePaymentWebhookAsync(string provider, string rawPayload, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        var signatureValid = _paymentGateway.VerifyWebhookSignature(rawPayload, headers);
        var parsed = _paymentGateway.ParseWebhook(rawPayload);

        var already = await _db.PaymentWebhooks
            .AnyAsync(w => w.Provider == provider && w.ExternalEventId == parsed.ExternalEventId, ct);

        var webhook = new PaymentWebhook
        {
            Provider = provider,
            ExternalEventId = parsed.ExternalEventId,
            EventType = parsed.EventType,
            RawPayload = rawPayload,
            SignatureValid = signatureValid
        };
        _db.PaymentWebhooks.Add(webhook);

        if (already || !signatureValid)
        {
            webhook.Processed = false;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var order = await _db.PaymentOrders.FirstOrDefaultAsync(o => o.ProviderOrderId == parsed.ProviderOrderId, ct);
        if (order is null)
        {
            await _db.SaveChangesAsync(ct);
            return;
        }
        webhook.PaymentOrderId = order.Id;

        if (order.Amount != parsed.Amount || order.Currency != parsed.Currency)
        {
            order.Status = PaymentOrderStatus.Failed;
            webhook.Processed = true;
            webhook.ProcessedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return; // amount/currency mismatch — never trust the webhook blindly (rule: validar importe, moneda e idempotencia)
        }

        order.Status = parsed.Succeeded ? PaymentOrderStatus.Succeeded : PaymentOrderStatus.Failed;
        _db.PaymentTransactions.Add(new PaymentTransaction
        {
            PaymentOrderId = order.Id,
            ProviderTransactionId = parsed.ExternalEventId,
            Status = order.Status,
            Amount = parsed.Amount,
            Currency = parsed.Currency
        });

        var recharge = await _db.Recharges.Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.PaymentOrderId == order.Id, ct);

        if (recharge is not null && recharge.Status is RechargeStatus.Pending or RechargeStatus.Processing)
        {
            if (parsed.Succeeded)
            {
                recharge.Status = RechargeStatus.Processing;
                await _db.SaveChangesAsync(ct);

                var movement = await _ledger.CreditAsync(new WalletMovementRequest(
                    WalletId: recharge.WalletId, Amount: recharge.Amount, Type: WalletTransactionType.Recharge,
                    Channel: recharge.Channel, PerformedByUserId: "system:payment-webhook",
                    PaymentMethod: recharge.PaymentMethod, ExternalReference: parsed.ExternalEventId,
                    IdempotencyKey: $"recharge:{recharge.Id}", RechargeId: recharge.Id,
                    CorrelationId: webhook.Id.ToString()), ct);

                recharge.Status = RechargeStatus.Completed;
                recharge.WalletTransactionId = movement.Id;
                recharge.CompletedAtUtc = DateTime.UtcNow;

                var wallet = recharge.Wallet ?? await _db.Wallets.FirstAsync(w => w.Id == recharge.WalletId, ct);
                await QueueRechargeNotificationAsync(recharge, wallet, movement, ct);
            }
            else
            {
                recharge.Status = RechargeStatus.Rejected;
                recharge.RejectionReason = "Pago rechazado por el proveedor.";
            }
        }

        webhook.Processed = true;
        webhook.ProcessedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task QueueRechargeNotificationAsync(Recharge recharge, Wallet wallet, WalletTransaction movement, CancellationToken ct)
    {
        var buyer = await _db.Buyers.FirstOrDefaultAsync(b => b.Id == wallet.BuyerId, ct);
        var recipient = await ResolveNotificationRecipientAsync(wallet.BuyerId, ct);
        if (recipient is null || buyer is null) return;

        var body = $"Recarga completada para {buyer.FullName}. Monto: {movement.Amount:0.00} {wallet.Currency}. " +
                   $"Balance anterior: {movement.BalanceBefore:0.00}. Balance actual: {movement.BalanceAfter:0.00}. " +
                   $"Transacción: {movement.TransactionNumber}.";

        await _notifications.EnqueueAsync(
            wallet.SchoolId, NotificationEvent.RechargeCompleted, NotificationChannel.Email, recipient,
            "Recarga completada", body, movement.Id.ToString(), $"RechargeCompleted:{recharge.Id}", ct);
    }

    private async Task<string?> ResolveNotificationRecipientAsync(Guid buyerId, CancellationToken ct)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.BuyerId == buyerId, ct);
        if (student is not null)
        {
            var primaryGuardianLink = await _db.GuardianStudents
                .Where(gs => gs.StudentId == student.Id && gs.IsPrimary)
                .FirstOrDefaultAsync(ct);
            if (primaryGuardianLink is not null)
            {
                var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == primaryGuardianLink.GuardianId, ct);
                if (guardian is not null) return guardian.Email;
            }
            return student.StudentEmail;
        }

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.BuyerId == buyerId, ct);
        return employee?.Email;
    }

    private async Task<RechargeDto> ToDtoAsync(Recharge r, CancellationToken ct)
    {
        await Task.CompletedTask;
        return new RechargeDto(r.Id, r.WalletId, r.Amount, r.Currency, r.Status, r.Channel, r.PaymentMethod,
            r.CreatedAtUtc, r.CompletedAtUtc, r.RejectionReason);
    }
}
