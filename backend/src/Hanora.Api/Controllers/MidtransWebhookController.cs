using System.Text.Json;
using Hanora.Api.Security;
using Hanora.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
[EnableRateLimiting(RateLimitPolicies.Webhook)]
public sealed class MidtransWebhookController(IPaymentService paymentService)
    : ControllerBase
{
    [HttpPost("midtrans")]
    [AllowAnonymous]
    public async Task<IActionResult> Notify(
        [FromBody] MidtransNotificationRequest request,
        CancellationToken cancellationToken)
    {
        // Payload disimpan sebagai JSON untuk audit. Signature diverifikasi
        // dari field mentah yang dikirim Midtrans, bukan dari hasil parsing.
        var rawBody = JsonSerializer.Serialize(request);

        var result = await paymentService.HandleWebhookAsync(
            request, rawBody, cancellationToken);

        return StatusCode(result.StatusCode, new { message = result.Message });
    }
}
