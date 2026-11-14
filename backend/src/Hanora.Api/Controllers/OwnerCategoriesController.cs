using Hanora.Application.Abstractions;
using Hanora.Application.Catalog;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/owner/categories")]
[Authorize(Policy = Policies.OwnerOnly)]
public sealed class OwnerCategoriesController(ICatalogService catalogService) : StaffControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var categories = await catalogService.GetOwnerCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await catalogService.CreateCategoryAsync(
            request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : FailureProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await catalogService.UpdateCategoryAsync(
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

        var result = await catalogService.DeleteCategoryAsync(
            id, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? NoContent() : FailureProblem(result);
    }
}
