using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.DTOs;

public record ProductCategoryDto(Guid Id, string Name, string? Description);
public record CreateProductCategoryRequest(string Name, string? Description);

public record ProductDto(
    Guid Id, string Code, string? BarCode, string Name, string? Description, Guid CategoryId, string CategoryName,
    string? ImageUrl, UnitOfMeasure UnitOfMeasure, decimal Cost, decimal BasePrice, decimal TaxRate,
    ProductStatus Status, bool AvailableForSale, bool TrackInventory, decimal MinStockLevel, decimal ReorderLevel,
    string? Allergens, decimal? StockOnHand);

public record CreateProductRequest(
    string Code, string? BarCode, string Name, string? Description, Guid CategoryId, UnitOfMeasure UnitOfMeasure,
    decimal Cost, decimal BasePrice, decimal TaxRate, bool TrackInventory, decimal MinStockLevel,
    decimal ReorderLevel, string? Allergens);

public record UpdateProductRequest(
    string Name, string? Description, decimal Cost, decimal BasePrice, decimal TaxRate,
    ProductStatus Status, bool AvailableForSale, bool TrackInventory, decimal MinStockLevel, decimal ReorderLevel);

public record ScheduleProductPriceRequest(Guid ProductId, decimal UnitPrice, DateTime ValidFromUtc, DateTime? ValidToUtc);

public record WarehouseDto(Guid Id, string Name, bool IsActive);
public record CreateWarehouseRequest(string Name);

public record InventoryBalanceDto(Guid WarehouseId, string WarehouseName, Guid ProductId, string ProductName,
    string ProductCode, decimal QuantityOnHand, decimal MinStockLevel, bool IsLow);

public record InventoryMovementDto(Guid Id, Guid WarehouseId, Guid ProductId, string ProductName,
    InventoryMovementType Type, decimal Quantity, decimal BalanceAfter, string? Reference, string? Reason,
    string PerformedByUserId, DateTime OccurredAtUtc);

public record InventoryAdjustmentRequest(Guid WarehouseId, Guid ProductId, decimal Quantity, string Reason);
public record InventoryTransferRequest(Guid FromWarehouseId, Guid ToWarehouseId, Guid ProductId, decimal Quantity, string Reason);
public record InventoryEntryRequest(Guid WarehouseId, Guid ProductId, decimal Quantity, string? Reference, string? Reason);
