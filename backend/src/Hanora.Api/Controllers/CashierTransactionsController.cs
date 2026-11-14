using Hanora.Application.Abstractions;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/cashier/transactions")]
[Authorize(Policy = Policies.OwnerOrCashier)]
public sealed class CashierTransactionsController(IOrderService orderService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? method,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var transactions = await orderService.GetTransactionsAsync(
            from, to, method, status, page, pageSize, cancellationToken);

        return Ok(transactions);
    }
}
