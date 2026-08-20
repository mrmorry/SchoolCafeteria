using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class Recharge : SchoolScopedEntity
{
    public Guid WalletId { get; set; }
    public Wallet? Wallet { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public RechargeStatus Status { get; set; } = RechargeStatus.Pending;
    public WalletTransactionChannel Channel { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestedByUserId { get; set; } = string.Empty;

    public Guid? PaymentOrderId { get; set; }
    public PaymentOrder? PaymentOrder { get; set; }

    public Guid? WalletTransactionId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

/// <summary>Order handed to the payment gateway abstraction. Never stores card data — provider tokenizes.</summary>
public class PaymentOrder : SchoolScopedEntity
{
    public string Provider { get; set; } = "sandbox";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.Pending;

    [Sensitive]
    public string? ProviderCheckoutReference { get; set; }

    public string? ProviderOrderId { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
    public ICollection<PaymentWebhook> Webhooks { get; set; } = new List<PaymentWebhook>();
}

public class PaymentTransaction : BaseEntity
{
    public Guid PaymentOrderId { get; set; }
    public PaymentOrder? PaymentOrder { get; set; }

    public string ProviderTransactionId { get; set; } = string.Empty;
    public PaymentOrderStatus Status { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? FailureReason { get; set; }
}

/// <summary>Unique (Provider, ExternalEventId) guarantees a repeated webhook never duplicates a recharge.</summary>
public class PaymentWebhook : BaseEntity
{
    public Guid? PaymentOrderId { get; set; }
    public PaymentOrder? PaymentOrder { get; set; }

    public string Provider { get; set; } = "sandbox";
    public string ExternalEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    [Sensitive]
    public string RawPayload { get; set; } = string.Empty;

    public bool SignatureValid { get; set; }
    public bool Processed { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
