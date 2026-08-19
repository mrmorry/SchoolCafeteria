namespace SchoolCafeteria.Domain.Enums;

public enum BuyerType { Student, Teacher, AdminEmployee }

public enum StudentStatus { Active, Inactive, Suspended, Graduated }

public enum EmployeeStatus { Active, Inactive }

public enum WalletStatus { Active, Blocked, Closed }

public enum WalletTransactionType
{
    Recharge,
    Purchase,
    Refund,
    AdjustmentPositive,
    AdjustmentNegative,
    Reversal,
    Hold,
    HoldRelease,
    Expiration
}

public enum WalletTransactionStatus { Completed, Reversed, Cancelled }

public enum WalletTransactionChannel
{
    PointOfSale,
    CashierOffice,
    GuardianPortal,
    StudentPortal,
    Api,
    AdminAdjustment
}

public enum PaymentMethod
{
    Cash,
    CreditCard,
    DebitCard,
    BankTransfer,
    OnlinePayment,
    InstitutionalCredit,
    Other
}

public enum RechargeStatus
{
    Pending,
    Processing,
    Completed,
    Rejected,
    Cancelled,
    Reverted,
    InReconciliation
}

public enum PaymentOrderStatus { Pending, Processing, Succeeded, Failed, Cancelled, Expired }

public enum RfidCredentialStatus { Active, Blocked, Lost, Replaced, Inactive }

public enum ProductStatus { Active, Inactive, Discontinued }

public enum UnitOfMeasure { Unit, Kilogram, Gram, Liter, Milliliter, Package }

public enum SaleStatus { Completed, Cancelled, Refunded }

public enum ShiftStatus { Open, Closed }

public enum InventoryMovementType
{
    PurchaseIn,
    SaleOut,
    Transfer,
    AdjustmentIn,
    AdjustmentOut,
    Return,
    StockCountCorrection
}

public enum StockCountStatus { Draft, InProgress, Completed, Cancelled }

public enum NotificationChannel { Email, InApp, Sms, WhatsApp }

public enum NotificationStatus { Pending, Sent, Failed, DeadLettered }

public enum NotificationEvent
{
    RechargeCompleted,
    RechargeRejected,
    PurchaseCompleted,
    Refund,
    LowBalance,
    RfidBlocked,
    RfidReplaced,
    ImportCompleted,
    LowInventory,
    OutOfStock,
    ShiftClosedWithDifference,
    SensitiveAdminOperation
}

public enum ImportJobStatus { Uploaded, Validating, Validated, Importing, Completed, Failed }

public enum ImportRowStatus { Valid, Error, Duplicate, Imported, Skipped }

public enum ImportMode { Create, CreateOrUpdate, Deactivate }
