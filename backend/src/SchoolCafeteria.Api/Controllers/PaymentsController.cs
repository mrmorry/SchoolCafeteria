using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

/// <summary>Receives signed webhooks from the payment provider. Anonymous by design (the provider
/// cannot present a bearer token) but every payload is signature-verified inside RechargeService
/// before anything is trusted or persisted as completed.</summary>
[ApiController]
[Route("api/v1/payments/webhooks")]
[AllowAnonymous]
public class PaymentsController : ControllerBase
{
    private readonly RechargeService _rechargeService;
    public PaymentsController(RechargeService rechargeService) => _rechargeService = rechargeService;

    [HttpPost("{provider}")]
    public async Task<IActionResult> Handle(string provider, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(ct);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        await _rechargeService.HandlePaymentWebhookAsync(provider, rawPayload, headers, ct);
        return Ok(); // always 200 to a well-formed request so the provider does not endlessly retry;
                      // authenticity/idempotency failures are recorded, not surfaced as HTTP errors.
    }
}
