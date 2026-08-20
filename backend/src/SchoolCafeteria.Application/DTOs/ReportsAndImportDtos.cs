namespace SchoolCafeteria.Application.DTOs;

public record ReportFilter(DateTime FromUtc, DateTime ToUtc, Guid? PointOfSaleId = null, Guid? BuyerId = null,
    string? Channel = null, string? PaymentMethod = null, int Page = 1, int PageSize = 50);

public record RechargeReportRow(DateTime OccurredAtUtc, string TransactionNumber, string BuyerName,
    decimal Amount, string Channel, string PaymentMethod, string PerformedBy);

public record SalesReportRow(DateTime OccurredAtUtc, string SaleNumber, string BuyerName, string PointOfSale,
    string Operator, decimal Subtotal, decimal Tax, decimal Total);

public record LowStockReportRow(string ProductCode, string ProductName, string Warehouse, decimal QuantityOnHand,
    decimal MinStockLevel);

public record CashDifferenceReportRow(DateTime ClosedAtUtc, string Register, string Operator, decimal Expected,
    decimal Counted, decimal Difference);

public record DashboardSummaryDto(decimal TodaySales, decimal TodayRecharges, int TodayTransactions,
    int LowStockProducts, int ActiveWallets, decimal TotalWalletBalance);

// Import
public record ImportPreviewRowDto(int RowNumber, string NaturalKey, string Status, string? ErrorMessage, string RawDataJson);
public record ImportJobDto(Guid Id, string FileName, string Status, int TotalRows, int ValidRows, int ErrorRows,
    int DuplicateRows, int ImportedRows, DateTime CreatedAtUtc, DateTime? CompletedAtUtc, string? ResultFileUrl);
public record ConfirmImportRequest(Guid ImportJobId);
