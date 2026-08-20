using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;
using SchoolCafeteria.UnitTests.TestSupport;
using Xunit;

namespace SchoolCafeteria.UnitTests;

public class SaleServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly Guid _schoolId = Guid.NewGuid();
    private Guid _buyerId, _productId, _shiftId;

    public SaleServiceTests()
    {
        using var db = _factory.CreateContext();

        var buyer = new Buyer { SchoolId = _schoolId, Type = BuyerType.Student, FullName = "Comprador de Prueba" };
        db.Buyers.Add(buyer);
        db.Wallets.Add(new Wallet { SchoolId = _schoolId, BuyerId = buyer.Id, Currency = "USD", Balance = 50m });

        var category = new ProductCategory { SchoolId = _schoolId, Name = "Snacks" };
        db.ProductCategories.Add(category);
        var product = new Product { SchoolId = _schoolId, Code = "SNK-1", Name = "Galletas", CategoryId = category.Id, BasePrice = 2m, TaxRate = 0, TrackInventory = true, MinStockLevel = 1 };
        db.Products.Add(product);

        var warehouse = new Warehouse { SchoolId = _schoolId, Name = "Principal" };
        db.Warehouses.Add(warehouse);
        db.InventoryBalances.Add(new InventoryBalance { SchoolId = _schoolId, WarehouseId = warehouse.Id, ProductId = product.Id, QuantityOnHand = 5 });

        var pos = new PointOfSale { SchoolId = _schoolId, Name = "Cafetería", DefaultWarehouseId = warehouse.Id };
        db.PointsOfSale.Add(pos);
        var register = new Register { SchoolId = _schoolId, PointOfSaleId = pos.Id, Name = "Caja 1" };
        db.Registers.Add(register);
        var shift = new RegisterShift { SchoolId = _schoolId, RegisterId = register.Id, OperatorUserId = "op-1", Status = ShiftStatus.Open };
        db.RegisterShifts.Add(shift);

        db.SaveChanges();
        _buyerId = buyer.Id;
        _productId = product.Id;
        _shiftId = shift.Id;
    }

    private SaleService CreateSut(SchoolCafeteria.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var clock = new FixedDateTimeProvider();
        return new SaleService(db, new WalletLedgerService(db, clock), new InventoryLedgerService(db, clock),
            new SettingsService(db), new NotificationOutboxService(db), clock);
    }

    [Fact]
    public async Task Checkout_DebitsWalletAndReducesStock()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);

        var sale = await sut.CheckoutAsync(_schoolId, new CreateSaleRequest(_shiftId, _buyerId, null,
            new[] { new SaleLineRequest(_productId, 2, null) }, "sale-key-1"), "op-1");

        Assert.Equal(4m, sale.Total); // 2 units * $2, no tax
        Assert.Equal(46m, sale.BalanceAfter);

        var balance = db.InventoryBalances.Single(b => b.ProductId == _productId);
        Assert.Equal(3, balance.QuantityOnHand);
    }

    [Fact]
    public async Task Checkout_CalledTwiceWithSameIdempotencyKey_DoesNotDoubleCharge()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);
        var request = new CreateSaleRequest(_shiftId, _buyerId, null, new[] { new SaleLineRequest(_productId, 1, null) }, "double-click-key");

        var first = await sut.CheckoutAsync(_schoolId, request, "op-1");
        var second = await sut.CheckoutAsync(_schoolId, request, "op-1"); // simulates a double click on "Cobrar"

        Assert.Equal(first.Id, second.Id);
        var wallet = db.Wallets.Single(w => w.BuyerId == _buyerId);
        Assert.Equal(48m, wallet.Balance); // charged once ($2), not twice
    }

    [Fact]
    public async Task Checkout_InsufficientStock_RollsBackWalletDebit()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.CheckoutAsync(_schoolId, new CreateSaleRequest(
            _shiftId, _buyerId, null, new[] { new SaleLineRequest(_productId, 999, null) }, "sale-key-fail"), "op-1"));

        var wallet = db.Wallets.Single(w => w.BuyerId == _buyerId);
        Assert.Equal(50m, wallet.Balance); // no partial debit was left behind
        Assert.False(db.Sales.Any(s => s.IdempotencyKey == "sale-key-fail")); // no orphaned sale row either
    }

    public void Dispose() => _factory.Dispose();
}
