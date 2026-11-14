using Hanora.Application.Abstractions;
using Hanora.Application.Reporting;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/owner/expenses")]
[Authorize(Policy = Policies.OwnerOnly)]
public sealed class OwnerExpensesController(IExpenseService expenseService)
    : StaffControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!ReportPeriod.TryResolve(from, to, out var start, out var end, out var error))
        {
            return RequestProblem(error);
        }

        return Ok(await expenseService.ListAsync(
            start, end, page, pageSize, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await expenseService.CreateAsync(
            request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : FailureProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] ExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await expenseService.UpdateAsync(
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

        var result = await expenseService.DeleteAsync(
            id, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? NoContent() : FailureProblem(result);
    }
}
