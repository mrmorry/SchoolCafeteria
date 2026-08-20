using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class PointOfSale : SoftDeletableSchoolEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid DefaultWarehouseId { get; set; }

    public ICollection<Register> Registers { get; set; } = new List<Register>();
}

public class Register : SoftDeletableSchoolEntity
{
    public Guid PointOfSaleId { get; set; }
    public PointOfSale? PointOfSale { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class RegisterShift : SchoolScopedEntity
{
    public Guid RegisterId { get; set; }
    public Register? Register { get; set; }
    public string OperatorUserId { get; set; } = string.Empty;

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public decimal OpeningFloat { get; set; }
    public decimal? ClosingCounted { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CashDifference { get; set; }

    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosingNotes { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}

public class Sale : SchoolScopedEntity
{
    public string SaleNumber { get; set; } = string.Empty;
    public Guid RegisterShiftId { get; set; }
    public RegisterShift? RegisterShift { get; set; }
    public Guid PointOfSaleId { get; set; }
    public Guid BuyerId { get; set; }
    public Buyer? Buyer { get; set; }

    public string OperatorUserId { get; set; } = string.Empty;
    public string? RfidMaskedValueUsed { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }

    public Guid? WalletTransactionId { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<SaleLine> Lines { get; set; } = new List<SaleLine>();
}

public class SaleLine : BaseEntity
{
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductNameSnapshot { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}
