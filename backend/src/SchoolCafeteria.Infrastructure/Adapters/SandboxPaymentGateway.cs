using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SchoolCafeteria.Application.Abstractions;

namespace SchoolCafeteria.Infrastructure.Adapters;

/// <summary>
/// Reference implementation of IPaymentGateway: simulates a hosted-checkout PSP entirely inside
/// this process (no external network call), so the digital recharge flow is exercisable end to end
/// without a real payment provider contract. Signs webhook payloads with HMAC-SHA256 using a
/// shared secret from configuration, mirroring how a real provider would let you verify authenticity.
/// Replace with a real adapter (Stripe, local acquirer, etc.) purely via DI — nothing in
/// Application or the API depends on this class directly.
/// </summary>
public class SandboxPaymentGateway : IPaymentGateway
{
    private readonly string _secret;
    private readonly string _publicBaseUrl;

    public SandboxPaymentGateway(IConfiguration configuration)
    {
        _secret = configuration["Payments:Sandbox:WebhookSecret"] ?? "sandbox-dev-secret-change-me";
        _publicBaseUrl = configuration["Payments:Sandbox:CheckoutBaseUrl"] ?? "http://localhost:3000/pay/sandbox";
    }

    public string ProviderName => "sandbox";

    public Task<PaymentOrderResult> CreateOrderAsync(CreatePaymentOrderRequest request, CancellationToken ct = default)
    {
        var providerOrderId = $"sandbox_{request.PaymentOrderId:N}";
        var checkoutUrl = $"{_publicBaseUrl}?orderId={providerOrderId}&amount={request.Amount}&currency={request.Currency}&returnUrl={Uri.EscapeDataString(request.ReturnUrl)}";
        return Task.FromResult(new PaymentOrderResult(providerOrderId, checkoutUrl, DateTime.UtcNow.AddMinutes(30)));
    }

    public bool VerifyWebhookSignature(string rawPayload, IDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("X-Sandbox-Signature", out var signature)) return false;

        var expected = ComputeSignature(rawPayload);
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(signature));
        }
        catch (FormatException)
        {
            return false; // malformed signature header — never trust it
        }
    }

    public WebhookParseResult ParseWebhook(string rawPayload)
    {
        var payload = JsonSerializer.Deserialize<SandboxWebhookPayload>(rawPayload)
            ?? throw new InvalidOperationException("Payload de webhook sandbox inválido.");
        return new WebhookParseResult(payload.EventId, payload.EventType, payload.OrderId, payload.Succeeded, payload.Amount, payload.Currency);
    }

    public string ComputeSignature(string rawPayload)
    {
        var bytes = Encoding.UTF8.GetBytes(rawPayload);
        var keyBytes = Encoding.UTF8.GetBytes(_secret);
        return Convert.ToHexString(new HMACSHA256(keyBytes).ComputeHash(bytes)).ToLowerInvariant();
    }

    private record SandboxWebhookPayload(string EventId, string EventType, string OrderId, bool Succeeded, decimal Amount, string Currency);
}
