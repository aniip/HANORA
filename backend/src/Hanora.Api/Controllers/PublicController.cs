using Hanora.Api.Security;
using Hanora.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Public)]
public sealed class PublicController(
    ICatalogService catalogService,
    ISettingsService settingsService,
    ITableService tableService) : ControllerBase
{
    [HttpGet("site")]
    public async Task<IActionResult> GetSite(CancellationToken cancellationToken)
    {
        var site = await settingsService.GetPublicAsync(cancellationToken);
        return Ok(site);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await catalogService.GetPublicCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var products = await catalogService.GetPublicProductsAsync(
            category, search, page, pageSize, cancellationToken);

        return Ok(products);
    }

    [HttpGet("products/{slug}")]
    public async Task<IActionResult> GetProduct(
        string slug,
        CancellationToken cancellationToken)
    {
        var product = await catalogService.GetPublicProductAsync(slug, cancellationToken);

        return product is null
            ? NotFound(new { message = "Produk tidak ditemukan." })
            : Ok(product);
    }

    [HttpGet("tables/{qrToken}")]
    public async Task<IActionResult> ResolveTable(
        string qrToken,
        CancellationToken cancellationToken)
    {
        var session = await tableService.ResolveSessionAsync(qrToken, cancellationToken);

        return session is null
            ? NotFound(new { message = "QR meja tidak valid atau sudah diganti." })
            : Ok(session);
    }
}
