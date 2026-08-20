using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.DTOs;

public record PointOfSaleDto(Guid Id, string Name, string? Location, bool IsActive, IReadOnlyList<RegisterDto> Registers);
public record RegisterDto(Guid Id, string Name, bool IsActive);
public record CreatePointOfSaleRequest(string Name, string? Location, Guid DefaultWarehouseId);
public record CreateRegisterRequest(Guid PointOfSaleId, string Name);

public record OpenShiftRequest(Guid RegisterId, decimal OpeningFloat);
public record CloseShiftRequest(Guid ShiftId, decimal ClosingCounted, string? Notes);
public record ShiftDto(Guid Id, Guid RegisterId, string RegisterName, string OperatorUserId, ShiftStatus Status,
    decimal OpeningFloat, decimal? ClosingCounted, decimal? ExpectedCash, decimal? CashDifference,
    DateTime OpenedAtUtc, DateTime? ClosedAtUtc, decimal TotalSales, decimal TotalRecharges);

public record SaleLineRequest(Guid ProductId, decimal Quantity, decimal? DiscountAmount);
public record CreateSaleRequest(Guid ShiftId, Guid BuyerId, string? RfidMaskedValueUsed,
    IReadOnlyList<SaleLineRequest> Lines, string IdempotencyKey);

public record SaleLineDto(Guid ProductId, string ProductName, decimal Quantity, decimal UnitPrice,
    decimal TaxRate, decimal DiscountAmount, decimal LineTotal);

public record SaleDto(Guid Id, string SaleNumber, Guid BuyerId, string BuyerName, decimal Subtotal,
    decimal TaxTotal, decimal DiscountTotal, decimal Total, SaleStatus Status, string OperatorUserId,
    DateTime OccurredAtUtc, IReadOnlyList<SaleLineDto> Lines, decimal BalanceAfter);

public record CancelSaleRequest(Guid SaleId, string Reason);
