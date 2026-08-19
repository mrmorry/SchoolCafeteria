using SchoolCafeteria.Application.Abstractions;

namespace SchoolCafeteria.UnitTests.TestSupport;

public class FakePaymentGateway : IPaymentGateway
{
    public string ProviderName => "fake";
    public bool NextSignatureValid { get; set; } = true;
    public WebhookParseResult? NextParseResult { get; set; }

    public Task<PaymentOrderResult> CreateOrderAsync(CreatePaymentOrderRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PaymentOrderResult($"fake_{request.PaymentOrderId:N}", "https://fake.local/checkout", DateTime.UtcNow.AddMinutes(30)));

    public bool VerifyWebhookSignature(string rawPayload, IDictionary<string, string> headers) => NextSignatureValid;

    public WebhookParseResult ParseWebhook(string rawPayload) => NextParseResult
        ?? throw new InvalidOperationException("Configure NextParseResult before calling ParseWebhook in a test.");
}
