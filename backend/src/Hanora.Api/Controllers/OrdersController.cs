using System.Security.Claims;
using Hanora.Api.Security;
using Hanora.Application.Abstractions;
using Hanora.Application.Auth;
using Hanora.Application.Orders;
using Hanora.Application.Payments;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[EnableRateLimiting(RateLimitPolicies.Orders)]
public sealed class OrdersController(
    IOrderService orderService,
    IPaymentService paymentService) : StaffControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.TableSession)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 64)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key diperlukan",
                detail: "Sertakan header Idempotency-Key (maksimal 64 karakter).",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "idempotency_key_required"
                });
        }

        var tableIdValue = User.FindFirstValue(AuthConstants.TableIdClaim);
        var qrCodeIdValue = User.FindFirstValue(AuthConstants.QrCodeIdClaim);
        var tableNumber = User.FindFirstValue(AuthConstants.TableNumberClaim) ?? string.Empty;

        if (!Guid.TryParse(tableIdValue, out var tableId) ||
            !Guid.TryParse(qrCodeIdValue, out var qrCodeId))
        {
            return UnauthorizedProblem();
        }

        var result = await orderService.CreateAsync(
            tableId,
            qrCodeId,
            tableNumber,
            idempotencyKey,
            request,
            GetIpAddress(),
            cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : FailureProblem(result);
    }

    [HttpGet("track/{trackingToken}")]
    [AllowAnonymous]
    public async Task<IActionResult> Track(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        var order = await orderService.GetTrackingAsync(trackingToken, cancellationToken);

        return order is null
            ? NotFound(new { message = "Pesanan tidak ditemukan." })
            : Ok(order);
    }

    [HttpPost("track/{trackingToken}/cancel")]
    [AllowAnonymous]
    public async Task<IActionResult> Cancel(
        string trackingToken,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        CancelOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await orderService.CancelAsync(
            trackingToken,
            request?.Reason,
            GetIpAddress(),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }

    [HttpPost("track/{trackingToken}/payments/qris")]
    [AllowAnonymous]
    public async Task<IActionResult> InitiateQrisPayment(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        var result = await paymentService.InitiateQrisAsync(
            trackingToken, cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }

    [HttpPost("track/{trackingToken}/simulate")]
    [AllowAnonymous]
    public async Task<IActionResult> SimulateQrisPayment(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        var result = await paymentService.SimulateAsync(
            trackingToken, cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }
}
