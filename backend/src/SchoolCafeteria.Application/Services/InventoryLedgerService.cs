using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// Choke point for InventoryBalance changes. Same pattern as WalletLedgerService: append-only
/// InventoryMovement + a materialized InventoryBalance kept consistent inside one transaction,
/// with a bounded optimistic-concurrency retry loop.
/// </summary>
public class InventoryLedgerService
{
    private const int MaxConcurrencyRetries = 3;

    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public InventoryLedgerService(IAppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<InventoryMovement> ApplyAsync(
        Guid schoolId, Guid warehouseId, Guid productId, decimal quantityDelta, InventoryMovementType type,
        string performedByUserId, bool allowNegativeStock, string? reference = null, string? reason = null,
        Guid? saleId = null, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            var balance = await _db.InventoryBalances
                .FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ProductId == productId, ct);

            if (balance is null)
            {
                balance = new InventoryBalance { SchoolId = schoolId, WarehouseId = warehouseId, ProductId = productId, QuantityOnHand = 0 };
                _db.InventoryBalances.Add(balance);
            }

            var newQuantity = balance.QuantityOnHand + quantityDelta;
            if (newQuantity < 0 && !allowNegativeStock)
                throw new BusinessRuleException("inventory.insufficient_stock", "No hay existencias suficientes del producto.");

            balance.QuantityOnHand = newQuantity;

            var movement = new InventoryMovement
            {
                SchoolId = schoolId,
                WarehouseId = warehouseId,
                ProductId = productId,
                Type = type,
                Quantity = quantityDelta,
                BalanceAfter = newQuantity,
                SaleId = saleId,
                Reference = reference,
                Reason = reason,
                PerformedByUserId = performedByUserId,
                OccurredAtUtc = _clock.UtcNow
            };
            _db.InventoryMovements.Add(movement);

            try
            {
                await _db.SaveChangesAsync(ct);
                return movement;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                _db.InventoryMovements.Entry(movement).State = EntityState.Detached;
                if (_db.InventoryBalances.Entry(balance).State != EntityState.Detached)
                    _db.InventoryBalances.Entry(balance).State = EntityState.Detached;
            }
        }

        throw new ConflictException("No fue posible actualizar el inventario por alta concurrencia. Intente nuevamente.");
    }
}
