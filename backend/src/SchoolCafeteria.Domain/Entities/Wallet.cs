using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class Wallet : SchoolScopedEntity
{
    public Guid BuyerId { get; set; }
    public Buyer? Buyer { get; set; }

    public string Currency { get; set; } = "USD";
    public decimal Balance { get; set; }
    public decimal HeldBalance { get; set; }
    public WalletStatus Status { get; set; } = WalletStatus.Active;

    public decimal? MaxBalance { get; set; }
    public decimal? LowBalanceThreshold { get; set; }
    public DateTime? LastLowBalanceAlertAtUtc { get; set; }

    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}

/// <summary>Immutable ledger entry. Never updated after creation — corrections are compensating rows.</summary>
public class WalletTransaction : BaseEntity
{
    public Guid SchoolId { get; set; }
    public Guid WalletId { get; set; }
    public Wallet? Wallet { get; set; }

    public string TransactionNumber { get; set; } = string.Empty;
    public WalletTransactionType Type { get; set; }
    public WalletTransactionStatus Status { get; set; } = WalletTransactionStatus.Completed;
    public WalletTransactionChannel Channel { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    public string PerformedByUserId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? PointOfSaleId { get; set; }
    public Guid? RegisterId { get; set; }
    public string? ExternalReference { get; set; }
    public string? Comment { get; set; }
    public string? Reason { get; set; }

    public Guid? RelatedTransactionId { get; set; }
    public WalletTransaction? RelatedTransaction { get; set; }

    public string? IdempotencyKey { get; set; }
    public string? CorrelationId { get; set; }

    public Guid? SaleId { get; set; }
    public Guid? RechargeId { get; set; }
}
