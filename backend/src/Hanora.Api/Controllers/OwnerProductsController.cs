using Hanora.Application.Abstractions;
using Hanora.Application.Catalog;
using Hanora.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/owner/products")]
[Authorize(Policy = Policies.OwnerOnly)]
public sealed class OwnerProductsController(
    ICatalogService catalogService,
    IProductImageService productImageService) : StaffControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var products = await catalogService.GetOwnerProductsAsync(
            categoryId, search, status, page, pageSize, cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var product = await catalogService.GetOwnerProductAsync(id, cancellationToken);

        return product is null
            ? NotFound(new { message = "Produk tidak ditemukan." })
            : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await catalogService.CreateProductAsync(
            request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : FailureProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await catalogService.UpdateProductAsync(
            id, request, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await catalogService.ArchiveProductAsync(
            id, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? NoContent() : FailureProblem(result);
    }

    [HttpPatch("{id:guid}/availability")]
    public async Task<IActionResult> UpdateAvailability(
        Guid id,
        [FromBody] ProductAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        var result = await catalogService.SetAvailabilityAsync(
            id, request.IsAvailable, actorId.Value, GetIpAddress(), cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }

    [HttpPost("{id:guid}/image")]
    public async Task<IActionResult> UploadImage(
        Guid id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var actorId = GetUserId();
        if (actorId is null)
        {
            return UnauthorizedProblem();
        }

        if (file is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Gambar tidak ditemukan",
                detail: "Pilih berkas gambar terlebih dahulu.");
        }

        await using var stream = file.OpenReadStream();
        var result = await productImageService.UploadAsync(
            id,
            stream,
            file.Length,
            file.ContentType,
            file.FileName,
            actorId.Value,
            GetIpAddress(),
            cancellationToken);

        return result.Succeeded ? Ok(result.Value) : FailureProblem(result);
    }
}
