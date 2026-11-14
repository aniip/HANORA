using Hanora.Application.Abstractions;
using Hanora.Application.Reporting;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/owner/reports")]
[Authorize(Policy = Policies.OwnerOnly)]
public sealed class OwnerReportsController(
    IReportingService reportingService,
    IReportExportService exportService) : StaffControllerBase
{
    [HttpGet("data")]
    public async Task<IActionResult> Data(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        if (!ReportPeriod.TryResolve(from, to, out var start, out var end, out var error))
        {
            return RequestProblem(error);
        }

        return Ok(await reportingService.GetReportDataAsync(
            start, end, cancellationToken));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string format = "pdf",
        [FromQuery] string type = "full",
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!ReportPeriod.TryResolve(from, to, out var start, out var end, out var error))
        {
            return RequestProblem(error);
        }

        var result = await exportService.ExportAsync(
            format, type, start, end, cancellationToken);

        return result.Succeeded && result.Value is not null
            ? File(
                result.Value.Content,
                result.Value.ContentType,
                result.Value.FileName)
            : FailureProblem(result);
    }
}
