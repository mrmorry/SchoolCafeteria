using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class Warehouse : SoftDeletableSchoolEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<InventoryBalance> Balances { get; set; } = new List<InventoryBalance>();
}

public class InventoryBalance : BaseEntity
{
    public Guid SchoolId { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal QuantityOnHand { get; set; }
}

/// <summary>Append-only kardex. Current balance is a materialized projection kept consistent inside the same transaction.</summary>
public class InventoryMovement : BaseEntity
{
    public Guid SchoolId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }

    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }

    public Guid? SaleId { get; set; }
    public Guid? StockCountId { get; set; }
    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public string PerformedByUserId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public class StockCount : SchoolScopedEntity
{
    public Guid WarehouseId { get; set; }
    public StockCountStatus Status { get; set; } = StockCountStatus.Draft;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();
}

public class StockCountLine : BaseEntity
{
    public Guid StockCountId { get; set; }
    public StockCount? StockCount { get; set; }
    public Guid ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Variance => CountedQuantity - SystemQuantity;
}
