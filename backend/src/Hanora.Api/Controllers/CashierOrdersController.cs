using Hanora.Application.Abstractions;
using Hanora.Application.Orders;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/cashier/orders")]
[Authorize(Policy = Policies.OwnerOrCashier)]
public sealed class CashierOrdersController(IOrderService orderService) : StaffControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orders = await orderService.GetCashierOrdersAsync(
            status, from, to, page, pageSize, cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await orderService.GetCashierOrderAsync(id, cancellationToken);

        return order is null
            ? NotFound(new { message = "Pesanan tidak ditemukan." })
            : Ok(order);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await orderService.UpdateStatusAsync(
            id, request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }

    [HttpPost("{id:guid}/confirm-cash")]
    public async Task<IActionResult> ConfirmCash(
        Guid id,
        [FromBody] ConfirmCashRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await orderService.ConfirmCashAsync(
            id, request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelCashierOrderRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await orderService.CancelByCashierAsync(
            id, request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }
}

