using Hanora.Application.Abstractions;
using Hanora.Application.Catalog;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/owner/settings")]
[Authorize(Policy = Policies.OwnerOnly)]
public sealed class OwnerSettingsController(ISettingsService settingsService) : StaffControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetOwnerAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] SiteSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await settingsService.UpdateAsync(
            request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }
}
