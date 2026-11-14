using Hanora.Application.Abstractions;
using Hanora.Application.Security;
using Hanora.Application.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/owner/tables")]
[Authorize(Policy = Policies.OwnerOnly)]
public sealed class OwnerTablesController(ITableService tableService) : StaffControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tables = await tableService.ListAsync(cancellationToken);
        return Ok(tables);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TableRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await tableService.CreateAsync(
            request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : FailureProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] TableRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await tableService.UpdateAsync(
            id, request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await tableService.DeleteAsync(
            id, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? NoContent() : FailureProblem(result);
    }

    [HttpGet("{id:guid}/qr")]
    public async Task<IActionResult> GetQr(
        Guid id,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var normalized = format?.Trim().ToLowerInvariant() ?? "png";
        if (normalized is not ("png" or "svg"))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Format tidak didukung",
                detail: "Format QR harus png atau svg.");
        }

        var result = await tableService.GetQrAsync(id, normalized, cancellationToken);

        return result.Succeeded && result.Value is not null
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : FailureProblem(result);
    }

    [HttpPost("{id:guid}/qr/regenerate")]
    public async Task<IActionResult> RegenerateQr(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await tableService.RegenerateQrAsync(
            id, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }
}
