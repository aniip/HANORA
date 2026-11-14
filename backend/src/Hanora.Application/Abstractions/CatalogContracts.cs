using Hanora.Application.Catalog;
using Hanora.Application.Common;

namespace Hanora.Application.Abstractions;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetPublicCategoriesAsync(CancellationToken cancellationToken);

    Task<PagedResult<ProductDto>> GetPublicProductsAsync(
        string? categorySlug,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ProductDto?> GetPublicProductAsync(string slug, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryDto>> GetOwnerCategoriesAsync(CancellationToken cancellationToken);

    Task<ServiceResult<CategoryDto>> CreateCategoryAsync(
        CategoryRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<CategoryDto>> UpdateCategoryAsync(
        Guid categoryId,
        CategoryRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteCategoryAsync(
        Guid categoryId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<PagedResult<ProductDto>> GetOwnerProductsAsync(
        Guid? categoryId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ProductDto?> GetOwnerProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<ServiceResult<ProductDto>> CreateProductAsync(
        ProductRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProductDto>> UpdateProductAsync(
        Guid productId,
        ProductRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<ProductDto>> SetAvailabilityAsync(
        Guid productId,
        bool isAvailable,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> ArchiveProductAsync(
        Guid productId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}

public interface ISettingsService
{
    Task<SiteSettingsDto> GetPublicAsync(CancellationToken cancellationToken);

    Task<SiteSettingsDto> GetOwnerAsync(CancellationToken cancellationToken);

    Task<ServiceResult<SiteSettingsDto>> UpdateAsync(
        SiteSettingsRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}

public interface IProductImageService
{
    Task<ServiceResult<ProductDto>> UploadAsync(
        Guid productId,
        Stream content,
        long length,
        string? contentType,
        string? fileName,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}
