namespace SchoolCafeteria.Application.Abstractions;

/// <summary>
/// Abstracts any payment acquirer/PSP. No implementation of this interface may ever receive or
/// store raw card data — real providers must use hosted checkout / tokenization. The concrete
/// provider is chosen purely by DI configuration (see appsettings "Payments:Provider").
/// </summary>
public interface IPaymentGateway
{
    string ProviderName { get; }

    Task<PaymentOrderResult> CreateOrderAsync(CreatePaymentOrderRequest request, CancellationToken ct = default);

    /// <summary>Validates the authenticity (signature) of an inbound webhook payload.</summary>
    bool VerifyWebhookSignature(string rawPayload, IDictionary<string, string> headers);

    /// <summary>Parses a verified webhook payload into a normalized result.</summary>
    WebhookParseResult ParseWebhook(string rawPayload);
}

public record CreatePaymentOrderRequest(Guid PaymentOrderId, decimal Amount, string Currency, string Description, string ReturnUrl);

public record PaymentOrderResult(string ProviderOrderId, string CheckoutUrl, DateTime ExpiresAtUtc);

public record WebhookParseResult(string ExternalEventId, string EventType, string ProviderOrderId, bool Succeeded, decimal Amount, string Currency);
