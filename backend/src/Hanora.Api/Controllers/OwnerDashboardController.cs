using Hanora.Application.Abstractions;
using Hanora.Application.Reporting;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/owner/dashboard")]
[Authorize(Policy = Policies.OwnerOnly)]
public sealed class OwnerDashboardController(IReportingService reportingService)
    : StaffControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!ReportPeriod.TryResolve(from, to, out var start, out var end, out var error))
        {
            return RequestProblem(error);
        }

        return Ok(await reportingService.GetSummaryAsync(
            start, end, cancellationToken));
    }

    [HttpGet("sales-trend")]
    public async Task<IActionResult> SalesTrend(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string groupBy = "day",
        CancellationToken cancellationToken = default)
    {
        if (!ReportPeriod.TryResolve(from, to, out var start, out var end, out var error))
        {
            return RequestProblem(error);
        }

        if (groupBy is not ("day" or "month"))
        {
            return RequestProblem("groupBy harus day atau month.");
        }

        return Ok(await reportingService.GetSalesTrendAsync(
            start, end, groupBy, cancellationToken));
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (!ReportPeriod.TryResolve(from, to, out var start, out var end, out var error))
        {
            return RequestProblem(error);
        }

        return Ok(await reportingService.GetTopProductsAsync(
            start, end, limit, cancellationToken));
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> PaymentMethods(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!ReportPeriod.TryResolve(from, to, out var start, out var end, out var error))
        {
            return RequestProblem(error);
        }

        return Ok(await reportingService.GetPaymentMethodsAsync(
            start, end, cancellationToken));
    }
}
