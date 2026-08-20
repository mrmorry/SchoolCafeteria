using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;
using SchoolCafeteria.UnitTests.TestSupport;
using Xunit;

namespace SchoolCafeteria.UnitTests;

public class InventoryLedgerServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly Guid _warehouseId;
    private readonly Guid _productId;

    public InventoryLedgerServiceTests()
    {
        using var db = _factory.CreateContext();
        var category = new ProductCategory { SchoolId = _schoolId, Name = "Bebidas" };
        db.ProductCategories.Add(category);
        var product = new Product { SchoolId = _schoolId, Code = "P-1", Name = "Agua", CategoryId = category.Id, BasePrice = 1m, MinStockLevel = 5 };
        db.Products.Add(product);
        var warehouse = new Warehouse { SchoolId = _schoolId, Name = "Principal" };
        db.Warehouses.Add(warehouse);
        db.InventoryBalances.Add(new InventoryBalance { SchoolId = _schoolId, WarehouseId = warehouse.Id, ProductId = product.Id, QuantityOnHand = 10 });
        db.SaveChanges();
        _warehouseId = warehouse.Id;
        _productId = product.Id;
    }

    [Fact]
    public async Task Apply_SaleOut_ReducesBalance()
    {
        using var db = _factory.CreateContext();
        var sut = new InventoryLedgerService(db, new FixedDateTimeProvider());

        var movement = await sut.ApplyAsync(_schoolId, _warehouseId, _productId, -3, InventoryMovementType.SaleOut, "op-1", allowNegativeStock: false);

        Assert.Equal(7, movement.BalanceAfter);
    }

    [Fact]
    public async Task Apply_BeyondAvailableStock_ThrowsAndLeavesBalanceUnchanged()
    {
        using var db = _factory.CreateContext();
        var sut = new InventoryLedgerService(db, new FixedDateTimeProvider());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sut.ApplyAsync(_schoolId, _warehouseId, _productId, -100, InventoryMovementType.SaleOut, "op-1", allowNegativeStock: false));

        var balance = db.InventoryBalances.Single(b => b.WarehouseId == _warehouseId && b.ProductId == _productId);
        Assert.Equal(10, balance.QuantityOnHand);
    }

    public void Dispose() => _factory.Dispose();
}
