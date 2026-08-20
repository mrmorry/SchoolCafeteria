using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>Administrative inventory operations (entries, adjustments, transfers) — distinct from the
/// automatic movements a Sale/Refund generates via InventoryLedgerService, but backed by the same ledger.</summary>
public class InventoryAdminService
{
    private readonly IAppDbContext _db;
    private readonly InventoryLedgerService _ledger;
    private readonly NotificationOutboxService _notifications;

    public InventoryAdminService(IAppDbContext db, InventoryLedgerService ledger, NotificationOutboxService notifications)
    {
        _db = db;
        _ledger = ledger;
        _notifications = notifications;
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(Guid schoolId, CreateWarehouseRequest request, CancellationToken ct = default)
    {
        var warehouse = new Warehouse { SchoolId = schoolId, Name = request.Name, IsActive = true };
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync(ct);
        return new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.IsActive);
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(Guid schoolId, CancellationToken ct = default) =>
        await _db.Warehouses.Where(w => w.SchoolId == schoolId && !w.IsDeleted)
            .Select(w => new WarehouseDto(w.Id, w.Name, w.IsActive)).ToListAsync(ct);

    public async Task<InventoryMovement> EntryAsync(Guid schoolId, InventoryEntryRequest request, string userId, CancellationToken ct = default) =>
        await _ledger.ApplyAsync(schoolId, request.WarehouseId, request.ProductId, Math.Abs(request.Quantity),
            InventoryMovementType.PurchaseIn, userId, allowNegativeStock: true, request.Reference, request.Reason, ct: ct);

    public async Task AdjustAsync(Guid schoolId, InventoryAdjustmentRequest request, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("inventory.reason_required", "Todo ajuste de inventario requiere un motivo.");

        var type = request.Quantity >= 0 ? InventoryMovementType.AdjustmentIn : InventoryMovementType.AdjustmentOut;
        await _ledger.ApplyAsync(schoolId, request.WarehouseId, request.ProductId, request.Quantity, type, userId,
            allowNegativeStock: false, reason: request.Reason, ct: ct);

        await CheckLowStockAsync(schoolId, request.WarehouseId, request.ProductId, ct);
    }

    public async Task TransferAsync(Guid schoolId, InventoryTransferRequest request, string userId, CancellationToken ct = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(ct);
        try
        {
            await _ledger.ApplyAsync(schoolId, request.FromWarehouseId, request.ProductId, -Math.Abs(request.Quantity),
                InventoryMovementType.Transfer, userId, allowNegativeStock: false, reason: request.Reason, ct: ct);
            await _ledger.ApplyAsync(schoolId, request.ToWarehouseId, request.ProductId, Math.Abs(request.Quantity),
                InventoryMovementType.Transfer, userId, allowNegativeStock: true, reason: request.Reason, ct: ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<InventoryBalanceDto>> GetBalancesAsync(Guid schoolId, Guid? warehouseId, bool lowStockOnly, CancellationToken ct = default)
    {
        var query = from b in _db.InventoryBalances
                     join w in _db.Warehouses on b.WarehouseId equals w.Id
                     join p in _db.Products on b.ProductId equals p.Id
                     where w.SchoolId == schoolId && (warehouseId == null || b.WarehouseId == warehouseId)
                     select new InventoryBalanceDto(w.Id, w.Name, p.Id, p.Name, p.Code, b.QuantityOnHand, p.MinStockLevel, b.QuantityOnHand <= p.MinStockLevel);

        var result = await query.ToListAsync(ct);
        return lowStockOnly ? result.Where(r => r.IsLow).ToList() : result;
    }

    private async Task CheckLowStockAsync(Guid schoolId, Guid warehouseId, Guid productId, CancellationToken ct)
    {
        var balance = await _db.InventoryBalances.FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ProductId == productId, ct);
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (balance is null || product is null) return;

        if (balance.QuantityOnHand <= 0)
        {
            await _notifications.EnqueueAsync(schoolId, NotificationEvent.OutOfStock, NotificationChannel.InApp, "finance-team",
                "Producto agotado", $"{product.Name} está agotado en el almacén.", Guid.NewGuid().ToString(),
                $"OutOfStock:{productId}:{warehouseId}:{DateTime.UtcNow:yyyyMMdd}", ct);
        }
        else if (balance.QuantityOnHand <= product.MinStockLevel)
        {
            await _notifications.EnqueueAsync(schoolId, NotificationEvent.LowInventory, NotificationChannel.InApp, "finance-team",
                "Inventario bajo", $"{product.Name} está por debajo del nivel mínimo ({balance.QuantityOnHand}/{product.MinStockLevel}).",
                Guid.NewGuid().ToString(), $"LowInventory:{productId}:{warehouseId}:{DateTime.UtcNow:yyyyMMdd}", ct);
        }
    }
}
