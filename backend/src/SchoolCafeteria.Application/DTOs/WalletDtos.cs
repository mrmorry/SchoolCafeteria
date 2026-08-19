using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.DTOs;

public record WalletDto(Guid Id, Guid BuyerId, string BuyerName, string Currency, decimal Balance,
    decimal HeldBalance, WalletStatus Status, decimal? MaxBalance, decimal? LowBalanceThreshold);

public record WalletTransactionDto(
    Guid Id, string TransactionNumber, WalletTransactionType Type, WalletTransactionStatus Status,
    WalletTransactionChannel Channel, PaymentMethod? PaymentMethod, decimal Amount, decimal BalanceBefore,
    decimal BalanceAfter, string PerformedByUserId, DateTime OccurredAtUtc, string? Comment, string? Reason,
    string? ExternalReference);

public record SetLowBalanceThresholdRequest(decimal? Threshold);

public record RechargePresentialRequest(
    Guid WalletId, decimal Amount, PaymentMethod PaymentMethod, string IdempotencyKey, string? Comment);

public record RechargeDigitalRequest(Guid WalletId, decimal Amount, string IdempotencyKey, string ReturnUrl);

public record RechargeDto(Guid Id, Guid WalletId, decimal Amount, string Currency, RechargeStatus Status,
    WalletTransactionChannel Channel, PaymentMethod PaymentMethod, DateTime CreatedAtUtc, DateTime? CompletedAtUtc,
    string? RejectionReason);

public record ManualAdjustmentRequest(Guid WalletId, decimal Amount, string Reason, bool IsPositive);

public record AuthorizeCancellationRequest(Guid SaleId, string Reason);
