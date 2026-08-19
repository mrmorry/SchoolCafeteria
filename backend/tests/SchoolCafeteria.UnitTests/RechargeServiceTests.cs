using SchoolCafeteria.Application.Abstractions;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;
using SchoolCafeteria.UnitTests.TestSupport;
using Xunit;

namespace SchoolCafeteria.UnitTests;

public class RechargeServiceTests : IDisposable
{
    private readonly SqliteContextFactory _factory = new();
    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly FakePaymentGateway _gateway = new();
    private Guid _walletId;

    public RechargeServiceTests()
    {
        using var db = _factory.CreateContext();
        var buyer = new Buyer { SchoolId = _schoolId, Type = BuyerType.Student, FullName = "Comprador de Prueba" };
        db.Buyers.Add(buyer);
        var wallet = new Wallet { SchoolId = _schoolId, BuyerId = buyer.Id, Currency = "USD", Balance = 0m };
        db.Wallets.Add(wallet);
        db.SaveChanges();
        _walletId = wallet.Id;
    }

    private RechargeService CreateSut(SchoolCafeteria.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var clock = new FixedDateTimeProvider();
        return new RechargeService(db, new WalletLedgerService(db, clock), _gateway, new NotificationOutboxService(db), clock);
    }

    [Fact]
    public async Task Webhook_ReceivedTwiceForSameEvent_OnlyCreditsWalletOnce()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);

        await sut.RechargeDigitalAsync(_schoolId,
            new RechargeDigitalRequest(_walletId, 25m, "digital-key-1", "https://app.local/return"), "guardian-1", WalletTransactionChannel.GuardianPortal);

        var order = db.PaymentOrders.Single();
        _gateway.NextSignatureValid = true;
        _gateway.NextParseResult = new WebhookParseResult("evt-001", "payment.succeeded", order.ProviderOrderId!, true, 25m, "USD");

        var payload = "{}";
        var headers = new Dictionary<string, string> { ["X-Signature"] = "irrelevant-for-fake" };

        await sut.HandlePaymentWebhookAsync("fake", payload, headers);
        await sut.HandlePaymentWebhookAsync("fake", payload, headers); // provider retries the same webhook delivery

        var wallet = db.Wallets.Single(w => w.Id == _walletId);
        Assert.Equal(25m, wallet.Balance); // credited once, not 50

        var webhookRows = db.PaymentWebhooks.Count(w => w.ExternalEventId == "evt-001");
        Assert.Equal(2, webhookRows); // both deliveries are recorded for traceability...
        Assert.Equal(1, db.WalletTransactions.Count(t => t.Type == WalletTransactionType.Recharge)); // ...but only one ledger entry exists
    }

    [Fact]
    public async Task Webhook_AmountMismatch_DoesNotCompleteRecharge()
    {
        using var db = _factory.CreateContext();
        var sut = CreateSut(db);

        await sut.RechargeDigitalAsync(_schoolId, new RechargeDigitalRequest(_walletId, 25m, "digital-key-2", "https://app.local/return"), "guardian-1", WalletTransactionChannel.GuardianPortal);
        var order = db.PaymentOrders.Single();

        _gateway.NextSignatureValid = true;
        _gateway.NextParseResult = new WebhookParseResult("evt-002", "payment.succeeded", order.ProviderOrderId!, true, 999m, "USD"); // tampered amount

        await sut.HandlePaymentWebhookAsync("fake", "{}", new Dictionary<string, string>());

        var wallet = db.Wallets.Single(w => w.Id == _walletId);
        Assert.Equal(0m, wallet.Balance); // never trust the webhook amount blindly
    }

    public void Dispose() => _factory.Dispose();
}
