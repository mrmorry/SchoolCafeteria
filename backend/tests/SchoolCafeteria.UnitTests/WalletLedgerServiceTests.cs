using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;
using SchoolCafeteria.UnitTests.TestSupport;
using Xunit;

namespace SchoolCafeteria.UnitTests;

public class WalletLedgerServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly Guid _walletId;

    public WalletLedgerServiceTests()
    {
        using var db = _factory.CreateContext();
        var buyer = new Buyer { SchoolId = _schoolId, Type = BuyerType.Student, FullName = "Estudiante de Prueba" };
        db.Buyers.Add(buyer);
        var wallet = new Wallet { SchoolId = _schoolId, BuyerId = buyer.Id, Currency = "USD", Balance = 10m };
        db.Wallets.Add(wallet);
        db.SaveChanges();
        _walletId = wallet.Id;
    }

    private WalletLedgerService CreateSut(SchoolCafeteria.Infrastructure.Persistence.ApplicationDbContext db) =>
        new(db, new FixedDateTimeProvider());

    [Fact]
    public async Task Debit_WithSufficientBalance_ReducesBalanceAndCreatesTransaction()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);

        var tx = await sut.DebitAsync(new WalletMovementRequest(
            _walletId, 3m, WalletTransactionType.Purchase, WalletTransactionChannel.PointOfSale, "operator-1"));

        Assert.Equal(3m, tx.Amount);
        Assert.Equal(10m, tx.BalanceBefore);
        Assert.Equal(7m, tx.BalanceAfter);

        var wallet = db.Wallets.Single(w => w.Id == _walletId);
        Assert.Equal(7m, wallet.Balance);
    }

    [Fact]
    public async Task Debit_ExceedingBalance_ThrowsBusinessRuleException_AndLeavesBalanceUnchanged()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.DebitAsync(new WalletMovementRequest(
            _walletId, 999m, WalletTransactionType.Purchase, WalletTransactionChannel.PointOfSale, "operator-1")));

        var wallet = db.Wallets.Single(w => w.Id == _walletId);
        Assert.Equal(10m, wallet.Balance); // rule 1: a purchase never leaves a negative balance
    }

    [Fact]
    public async Task Credit_CalledTwiceWithSameIdempotencyKey_OnlyAppliesOnce()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);
        var request = new WalletMovementRequest(
            _walletId, 5m, WalletTransactionType.Recharge, WalletTransactionChannel.CashierOffice, "operator-1",
            IdempotencyKey: "recharge-abc");

        var first = await sut.CreditAsync(request);
        var second = await sut.CreditAsync(request); // simulates a retried request / duplicated click

        Assert.Equal(first.Id, second.Id);
        var wallet = db.Wallets.Single(w => w.Id == _walletId);
        Assert.Equal(15m, wallet.Balance); // credited only once, not 20
    }

    [Fact]
    public async Task Credit_ExceedingMaxBalance_Throws()
    {
        using var db = _factory.CreateContext();
        var wallet = db.Wallets.Single(w => w.Id == _walletId);
        wallet.MaxBalance = 12m;
        db.SaveChanges();

        var sut = CreateSut(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => sut.CreditAsync(new WalletMovementRequest(
            _walletId, 10m, WalletTransactionType.Recharge, WalletTransactionChannel.CashierOffice, "operator-1")));
    }

    public void Dispose() => _factory.Dispose();
}
