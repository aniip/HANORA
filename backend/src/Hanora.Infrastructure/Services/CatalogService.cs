using Hanora.Application.Abstractions;
using Hanora.Application.Catalog;
using Hanora.Application.Common;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hanora.Infrastructure.Services;

public sealed class CatalogService(
    HanoraDbContext dbContext,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : ICatalogService
{
    public async Task<IReadOnlyList<CategoryDto>> GetPublicCategoriesAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategoryDto(
                x.Id,
                x.Name,
                x.Slug,
                x.SortOrder,
                x.IsActive,
                x.Products.Count(p => p.ArchivedAt == null)))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ProductDto>> GetPublicProductsAsync(
        string? categorySlug,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.IsAvailable &&
                        x.ArchivedAt == null &&
                        x.Category.IsActive);

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            var slug = categorySlug.Trim().ToLowerInvariant();
            query = query.Where(x => x.Category.Slug == slug);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, term) ||
                (x.Description != null && EF.Functions.ILike(x.Description, term)));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Category.SortOrder)
            .ThenBy(x => x.Category.Name)
            .ThenBy(x => x.Name)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(
            items.Select(MapProduct).ToList(),
            safePage,
            safePageSize,
            totalItems);
    }

    public async Task<ProductDto?> GetPublicProductAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x =>
                x.Slug == normalized &&
                x.IsAvailable &&
                x.ArchivedAt == null &&
                x.Category.IsActive,
                cancellationToken);

        return product is null ? null : MapProduct(product);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetOwnerCategoriesAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CategoryDto(
                x.Id,
                x.Name,
                x.Slug,
                x.SortOrder,
                x.IsActive,
                x.Products.Count(p => p.ArchivedAt == null)))
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<CategoryDto>> CreateCategoryAsync(
        CategoryRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<CategoryDto>.Failure(
                "validation", "Nama kategori wajib diisi.");
        }

        var slug = await GenerateUniqueCategorySlugAsync(
            string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug,
            null,
            cancellationToken);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = timeProvider.GetUtcNow(),
            UpdatedAt = timeProvider.GetUtcNow()
        };

        dbContext.Categories.Add(category);

        await auditWriter.WriteAsync(
            actorId,
            "category.created",
            "category",
            category.Id.ToString(),
            new { category.Name, category.Slug },
            ipAddress,
            cancellationToken);

        return ServiceResult<CategoryDto>.Success(MapCategory(category, 0));
    }

    public async Task<ServiceResult<CategoryDto>> UpdateCategoryAsync(
        Guid categoryId,
        CategoryRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return ServiceResult<CategoryDto>.Failure(
                "not_found", "Kategori tidak ditemukan.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<CategoryDto>.Failure(
                "validation", "Nama kategori wajib diisi.");
        }

        var slug = await GenerateUniqueCategorySlugAsync(
            string.IsNullOrWhiteSpace(request.Slug) ? name : request.Slug,
            categoryId,
            cancellationToken);

        category.Name = name;
        category.Slug = slug;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = timeProvider.GetUtcNow();

        await auditWriter.WriteAsync(
            actorId,
            "category.updated",
            "category",
            category.Id.ToString(),
            new { category.Name, category.Slug, category.IsActive },
            ipAddress,
            cancellationToken);

        var productCount = await dbContext.Products
            .CountAsync(x => x.CategoryId == categoryId && x.ArchivedAt == null, cancellationToken);

        return ServiceResult<CategoryDto>.Success(MapCategory(category, productCount));
    }

    public async Task<ServiceResult<bool>> DeleteCategoryAsync(
        Guid categoryId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return ServiceResult<bool>.Failure(
                "not_found", "Kategori tidak ditemukan.");
        }

        var hasProducts = await dbContext.Products
            .AnyAsync(x => x.CategoryId == categoryId, cancellationToken);

        if (hasProducts)
        {
            return ServiceResult<bool>.Failure(
                "category_in_use",
                "Kategori masih digunakan oleh produk. Pindahkan atau arsipkan produk terlebih dahulu.");
        }

        dbContext.Categories.Remove(category);

        await auditWriter.WriteAsync(
            actorId,
            "category.deleted",
            "category",
            category.Id.ToString(),
            new { category.Name, category.Slug },
            ipAddress,
            cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<PagedResult<ProductDto>> GetOwnerProductsAsync(
        Guid? categoryId,
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .AsQueryable();

        query = status?.Trim().ToLowerInvariant() switch
        {
            "available" => query.Where(x => x.ArchivedAt == null && x.IsAvailable),
            "unavailable" => query.Where(x => x.ArchivedAt == null && !x.IsAvailable),
            "archived" => query.Where(x => x.ArchivedAt != null),
            _ => query
        };

        if (categoryId is not null)
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, term) ||
                (x.Description != null && EF.Functions.ILike(x.Description, term)));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Name)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(
            items.Select(MapProduct).ToList(),
            safePage,
            safePageSize,
            totalItems);
    }

    public async Task<ProductDto?> GetOwnerProductAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

        return product is null ? null : MapProduct(product);
    }

    public async Task<ServiceResult<ProductDto>> CreateProductAsync(
        ProductRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return ServiceResult<ProductDto>.Failure(
                "category_not_found", "Kategori tidak ditemukan.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || request.Price <= 0)
        {
            return ServiceResult<ProductDto>.Failure(
                "validation", "Nama dan harga produk wajib valid.");
        }

        var slug = await GenerateUniqueProductSlugAsync(name, null, cancellationToken);
        var now = timeProvider.GetUtcNow();

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Name = name,
            Slug = slug,
            Description = NormalizeOptional(request.Description),
            Price = request.Price,
            IsAvailable = request.IsAvailable,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Products.Add(product);

        await auditWriter.WriteAsync(
            actorId,
            "product.created",
            "product",
            product.Id.ToString(),
            new { product.Name, product.Slug, product.Price },
            ipAddress,
            cancellationToken);

        return ServiceResult<ProductDto>.Success(MapProduct(product));
    }

    public async Task<ServiceResult<ProductDto>> UpdateProductAsync(
        Guid productId,
        ProductRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

        if (product is null)
        {
            return ServiceResult<ProductDto>.Failure(
                "not_found", "Produk tidak ditemukan.");
        }

        if (product.ArchivedAt is not null)
        {
            return ServiceResult<ProductDto>.Failure(
                "archived", "Produk yang sudah diarsipkan tidak dapat diubah.");
        }

        var category = await dbContext.Categories
            .FirstOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return ServiceResult<ProductDto>.Failure(
                "category_not_found", "Kategori tidak ditemukan.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || request.Price <= 0)
        {
            return ServiceResult<ProductDto>.Failure(
                "validation", "Nama dan harga produk wajib valid.");
        }

        product.Name = name;
        product.Slug = await GenerateUniqueProductSlugAsync(name, productId, cancellationToken);
        product.CategoryId = category.Id;
        product.Category = category;
        product.Description = NormalizeOptional(request.Description);
        product.Price = request.Price;
        product.IsAvailable = request.IsAvailable;
        product.UpdatedAt = timeProvider.GetUtcNow();

        await auditWriter.WriteAsync(
            actorId,
            "product.updated",
            "product",
            product.Id.ToString(),
            new { product.Name, product.Slug, product.Price, product.IsAvailable },
            ipAddress,
            cancellationToken);

        return ServiceResult<ProductDto>.Success(MapProduct(product));
    }

    public async Task<ServiceResult<ProductDto>> SetAvailabilityAsync(
        Guid productId,
        bool isAvailable,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

        if (product is null)
        {
            return ServiceResult<ProductDto>.Failure(
                "not_found", "Produk tidak ditemukan.");
        }

        if (product.ArchivedAt is not null)
        {
            return ServiceResult<ProductDto>.Failure(
                "archived", "Produk yang sudah diarsipkan tidak dapat diubah.");
        }

        product.IsAvailable = isAvailable;
        product.UpdatedAt = timeProvider.GetUtcNow();

        await auditWriter.WriteAsync(
            actorId,
            "product.availability_changed",
            "product",
            product.Id.ToString(),
            new { product.Name, product.IsAvailable },
            ipAddress,
            cancellationToken);

        return ServiceResult<ProductDto>.Success(MapProduct(product));
    }

    public async Task<ServiceResult<bool>> ArchiveProductAsync(
        Guid productId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

        if (product is null)
        {
            return ServiceResult<bool>.Failure(
                "not_found", "Produk tidak ditemukan.");
        }

        if (product.ArchivedAt is not null)
        {
            return ServiceResult<bool>.Success(true);
        }

        product.ArchivedAt = timeProvider.GetUtcNow();
        product.IsAvailable = false;
        product.UpdatedAt = product.ArchivedAt.Value;

        await auditWriter.WriteAsync(
            actorId,
            "product.archived",
            "product",
            product.Id.ToString(),
            new { product.Name },
            ipAddress,
            cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public static string? BuildImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        var value = imagePath.Trim();

        // Mendukung gambar lokal (/media/...) dan URL absolut (misalnya CDN)
        // agar data demo dapat memakai foto online tanpa mengubah kontrak API.
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps))
        {
            return value;
        }

        return $"/media/{value.TrimStart('/')}";
    }

    private async Task<string> GenerateUniqueCategorySlugAsync(
        string source,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        return await GenerateUniqueSlugAsync(
            source,
            candidate => dbContext.Categories.AnyAsync(
                x => x.Slug == candidate && (excludeId == null || x.Id != excludeId),
                cancellationToken));
    }

    private async Task<string> GenerateUniqueProductSlugAsync(
        string source,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        return await GenerateUniqueSlugAsync(
            source,
            candidate => dbContext.Products.AnyAsync(
                x => x.Slug == candidate && (excludeId == null || x.Id != excludeId),
                cancellationToken));
    }

    private static async Task<string> GenerateUniqueSlugAsync(
        string source,
        Func<string, Task<bool>> exists,
        int maxAttempts = 50)
    {
        var baseSlug = SlugHelper.Slugify(source);
        var candidate = baseSlug;

        for (var attempt = 2; attempt <= maxAttempts; attempt++)
        {
            if (!await exists(candidate))
            {
                return candidate;
            }

            candidate = $"{baseSlug}-{attempt}";
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(80, baseSlug.Length + 9)];
    }

    private static CategoryDto MapCategory(Category category, int productCount) =>
        new(
            category.Id,
            category.Name,
            category.Slug,
            category.SortOrder,
            category.IsActive,
            productCount);

    private static ProductDto MapProduct(Product product) =>
        new(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Price,
            BuildImageUrl(product.ImagePath),
            product.IsAvailable,
            product.ArchivedAt is not null,
            product.CategoryId,
            product.Category.Name,
            product.Category.Slug,
            product.CreatedAt,
            product.UpdatedAt);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
