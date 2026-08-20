using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// POS checkout. A sale, its wallet debit and its inventory movements are committed together in
/// one database transaction: if any step fails (insufficient balance, insufficient stock,
/// concurrency conflict) the whole sale rolls back — no partially recorded operation is possible.
/// </summary>
public class SaleService
{
    private readonly IAppDbContext _db;
    private readonly WalletLedgerService _walletLedger;
    private readonly InventoryLedgerService _inventoryLedger;
    private readonly SettingsService _settings;
    private readonly NotificationOutboxService _notifications;
    private readonly IDateTimeProvider _clock;

    public SaleService(
        IAppDbContext db, WalletLedgerService walletLedger, InventoryLedgerService inventoryLedger,
        SettingsService settings, NotificationOutboxService notifications, IDateTimeProvider clock)
    {
        _db = db;
        _walletLedger = walletLedger;
        _inventoryLedger = inventoryLedger;
        _settings = settings;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<SaleDto> CheckoutAsync(Guid schoolId, CreateSaleRequest request, string operatorUserId, CancellationToken ct = default)
    {
        var existingSale = await _db.Sales
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.RegisterShiftId == request.ShiftId && s.IdempotencyKey == request.IdempotencyKey, ct);
        if (existingSale is not null)
            return await ToDtoAsync(existingSale, ct); // double-click on "Cobrar" returns the already created sale

        if (request.Lines.Count == 0)
            throw new BusinessRuleException("sale.empty_cart", "El carrito no puede estar vacío.");

        var shift = await _db.RegisterShifts.Include(s => s.Register)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct)
            ?? throw new NotFoundException(nameof(RegisterShift), request.ShiftId);
        if (shift.Status != ShiftStatus.Open)
            throw new BusinessRuleException("sale.shift_closed", "La caja no tiene un turno abierto.");

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.BuyerId == request.BuyerId, ct)
            ?? throw new NotFoundException(nameof(Wallet), request.BuyerId);
        var buyer = await _db.Buyers.FirstOrDefaultAsync(b => b.Id == request.BuyerId, ct)
            ?? throw new NotFoundException(nameof(Buyer), request.BuyerId);

        var pointOfSaleId = shift.Register!.PointOfSaleId;
        var pos = await _db.PointsOfSale.FirstOrDefaultAsync(p => p.Id == pointOfSaleId, ct)
            ?? throw new NotFoundException(nameof(PointOfSale), pointOfSaleId);

        var allowSalesWithoutStock = await _settings.GetBoolAsync(schoolId, "pos.allow_sales_without_stock", false, ct);

        var sale = new Sale
        {
            SchoolId = schoolId,
            SaleNumber = $"SALE-{_clock.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            RegisterShiftId = shift.Id,
            PointOfSaleId = pointOfSaleId,
            BuyerId = request.BuyerId,
            OperatorUserId = operatorUserId,
            RfidMaskedValueUsed = request.RfidMaskedValueUsed,
            Status = SaleStatus.Completed,
            IdempotencyKey = request.IdempotencyKey,
            OccurredAtUtc = _clock.UtcNow
        };

        decimal subtotal = 0, taxTotal = 0, discountTotal = 0;
        var lines = new List<SaleLine>();

        foreach (var lineRequest in request.Lines)
        {
            if (lineRequest.Quantity <= 0)
                throw new BusinessRuleException("sale.invalid_quantity", "La cantidad debe ser mayor a cero.");

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == lineRequest.ProductId, ct)
                ?? throw new NotFoundException(nameof(Product), lineRequest.ProductId);
            if (product.Status != ProductStatus.Active || !product.AvailableForSale)
                throw new BusinessRuleException("sale.product_unavailable", $"El producto '{product.Name}' no está disponible para venta.");

            var unitPrice = await GetCurrentPriceAsync(product, ct);
            var discount = lineRequest.DiscountAmount ?? 0;
            var lineSubtotal = unitPrice * lineRequest.Quantity - discount;
            var lineTax = Math.Round(lineSubtotal * product.TaxRate, 2);
            var lineTotal = lineSubtotal + lineTax;

            subtotal += unitPrice * lineRequest.Quantity;
            discountTotal += discount;
            taxTotal += lineTax;

            lines.Add(new SaleLine
            {
                SaleId = sale.Id,
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                Quantity = lineRequest.Quantity,
                UnitPrice = unitPrice,
                TaxRate = product.TaxRate,
                DiscountAmount = discount,
                LineTotal = lineTotal
            });
        }

        sale.Subtotal = subtotal;
        sale.DiscountTotal = discountTotal;
        sale.TaxTotal = taxTotal;
        sale.Total = subtotal - discountTotal + taxTotal;

        await using var transaction = await _db.BeginTransactionAsync(ct);
        try
        {
            _db.Sales.Add(sale);
            foreach (var line in lines) _db.SaleLines.Add(line);
            await _db.SaveChangesAsync(ct);

            var walletMovement = await _walletLedger.DebitAsync(new WalletMovementRequest(
                WalletId: wallet.Id, Amount: sale.Total, Type: WalletTransactionType.Purchase,
                Channel: WalletTransactionChannel.PointOfSale, PerformedByUserId: operatorUserId,
                PointOfSaleId: pointOfSaleId, RegisterId: shift.RegisterId,
                IdempotencyKey: $"sale:{sale.Id}", SaleId: sale.Id), ct: ct);

            sale.WalletTransactionId = walletMovement.Id;

            foreach (var line in lines)
            {
                var product = await _db.Products.FirstAsync(p => p.Id == line.ProductId, ct);
                if (!product.TrackInventory) continue;

                await _inventoryLedger.ApplyAsync(
                    schoolId, pos.DefaultWarehouseId, product.Id, -line.Quantity, InventoryMovementType.SaleOut,
                    operatorUserId, allowSalesWithoutStock, reference: sale.SaleNumber, saleId: sale.Id, ct: ct);
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        await QueueSaleNotificationAsync(sale, lines, buyer, wallet, ct);
        return await ToDtoAsync(sale, ct, lines);
    }

    public async Task<SaleDto> CancelAsync(Guid schoolId, CancelSaleRequest request, string supervisorUserId, CancellationToken ct = default)
    {
        var sale = await _db.Sales.Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId, ct)
            ?? throw new NotFoundException(nameof(Sale), request.SaleId);
        if (sale.Status != SaleStatus.Completed)
            throw new BusinessRuleException("sale.not_cancellable", "La venta ya fue anulada o devuelta.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("sale.reason_required", "El motivo es obligatorio para anular una venta.");

        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.BuyerId == sale.BuyerId, ct)
            ?? throw new NotFoundException(nameof(Wallet), sale.BuyerId);
        var pos = await _db.PointsOfSale.FirstAsync(p => p.Id == sale.PointOfSaleId, ct);

        await using var transaction = await _db.BeginTransactionAsync(ct);
        try
        {
            await _walletLedger.CreditAsync(new WalletMovementRequest(
                WalletId: wallet.Id, Amount: sale.Total, Type: WalletTransactionType.Refund,
                Channel: WalletTransactionChannel.PointOfSale, PerformedByUserId: supervisorUserId,
                RelatedTransactionId: sale.WalletTransactionId, Reason: request.Reason,
                IdempotencyKey: $"sale-refund:{sale.Id}", SaleId: sale.Id), ct);

            foreach (var line in sale.Lines)
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
                if (product is { TrackInventory: true })
                {
                    await _inventoryLedger.ApplyAsync(
                        schoolId, pos.DefaultWarehouseId, product.Id, line.Quantity, InventoryMovementType.Return,
                        supervisorUserId, allowNegativeStock: true, reference: sale.SaleNumber, reason: request.Reason,
                        saleId: sale.Id, ct: ct);
                }
            }

            sale.Status = SaleStatus.Refunded;
            sale.CancellationReason = request.Reason;
            sale.CancelledAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return await ToDtoAsync(sale, ct, sale.Lines);
    }

    private async Task<decimal> GetCurrentPriceAsync(Product product, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var price = await _db.ProductPrices
            .Where(p => p.ProductId == product.Id && p.ValidFromUtc <= now && (p.ValidToUtc == null || p.ValidToUtc > now))
            .OrderByDescending(p => p.ValidFromUtc)
            .FirstOrDefaultAsync(ct);
        return price?.UnitPrice ?? product.BasePrice;
    }

    private async Task QueueSaleNotificationAsync(Sale sale, List<SaleLine> lines, Buyer buyer, Wallet wallet, CancellationToken ct)
    {
        var recipient = await ResolveRecipientAsync(buyer.Id, ct);
        if (recipient is null) return;

        var itemsText = string.Join(", ", lines.Select(l => $"{l.Quantity}x {l.ProductNameSnapshot}"));
        var body = $"Compra de {buyer.FullName}: {itemsText}. Total: {sale.Total:0.00} {wallet.Currency}. " +
                   $"Balance actual: {wallet.Balance:0.00}. Venta: {sale.SaleNumber}.";

        await _notifications.EnqueueAsync(
            sale.SchoolId, NotificationEvent.PurchaseCompleted, NotificationChannel.Email, recipient,
            "Compra realizada", body, sale.Id.ToString(), $"PurchaseCompleted:{sale.Id}", ct);
    }

    private async Task<string?> ResolveRecipientAsync(Guid buyerId, CancellationToken ct)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.BuyerId == buyerId, ct);
        if (student is not null)
        {
            var link = await _db.GuardianStudents.Where(gs => gs.StudentId == student.Id && gs.IsPrimary).FirstOrDefaultAsync(ct);
            if (link is not null)
            {
                var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == link.GuardianId, ct);
                if (guardian is not null) return guardian.Email;
            }
            return student.StudentEmail;
        }
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.BuyerId == buyerId, ct);
        return employee?.Email;
    }

    private async Task<SaleDto> ToDtoAsync(Sale sale, CancellationToken ct, ICollection<SaleLine>? linesOverride = null)
    {
        var buyer = await _db.Buyers.FirstAsync(b => b.Id == sale.BuyerId, ct);
        var wallet = await _db.Wallets.FirstAsync(w => w.BuyerId == sale.BuyerId, ct);
        var lines = linesOverride ?? sale.Lines.ToList();

        return new SaleDto(sale.Id, sale.SaleNumber, sale.BuyerId, buyer.FullName, sale.Subtotal, sale.TaxTotal,
            sale.DiscountTotal, sale.Total, sale.Status, sale.OperatorUserId, sale.OccurredAtUtc,
            lines.Select(l => new SaleLineDto(l.ProductId, l.ProductNameSnapshot, l.Quantity, l.UnitPrice, l.TaxRate, l.DiscountAmount, l.LineTotal)).ToList(),
            wallet.Balance);
    }
}
