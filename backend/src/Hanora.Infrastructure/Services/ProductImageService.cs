using Hanora.Application.Abstractions;
using Hanora.Application.Catalog;
using Hanora.Application.Common;
using Hanora.Infrastructure.Persistence;
using Hanora.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hanora.Infrastructure.Services;

public sealed class ProductImageService(
    HanoraDbContext dbContext,
    IOptions<StorageOptions> storageOptions,
    IHostEnvironment environment,
    IAuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<ProductImageService> logger) : IProductImageService
{
    private const long MaxFileSize = 2 * 1024 * 1024;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    public async Task<ServiceResult<ProductDto>> UploadAsync(
        Guid productId,
        Stream content,
        long length,
        string? contentType,
        string? fileName,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (length <= 0)
        {
            return ServiceResult<ProductDto>.Failure(
                "empty_file", "Berkas gambar kosong.");
        }

        if (length > MaxFileSize)
        {
            return ServiceResult<ProductDto>.Failure(
                "file_too_large", "Ukuran gambar maksimal 2 MB.");
        }

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

        var header = new byte[12];
        var bytesRead = await content.ReadAsync(header, cancellationToken);

        var imageFormat = DetectImageFormat(header.AsSpan(0, bytesRead));
        if (imageFormat is null)
        {
            return ServiceResult<ProductDto>.Failure(
                "invalid_image", "Format gambar harus JPEG, PNG, atau WebP.");
        }

        if (content.CanSeek)
        {
            content.Position = 0;
        }
        else
        {
            var buffered = new MemoryStream();
            await buffered.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
            await content.CopyToAsync(buffered, cancellationToken);
            buffered.Position = 0;
            content = buffered;
        }

        var uploadRoot = GetUploadRoot();
        var productDirectory = Path.Combine(uploadRoot, "products");
        Directory.CreateDirectory(productDirectory);

        var fileNameOnDisk = $"{Guid.NewGuid():N}{imageFormat.Value.Extension}";
        var fullPath = Path.Combine(productDirectory, fileNameOnDisk);

        await using (var fileStream = new FileStream(
                         fullPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         useAsync: true))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var oldImagePath = product.ImagePath;
        product.ImagePath = $"products/{fileNameOnDisk}";
        product.UpdatedAt = timeProvider.GetUtcNow();

        await auditWriter.WriteAsync(
            actorId,
            "product.image_updated",
            "product",
            product.Id.ToString(),
            new { fileName = fileNameOnDisk, contentType, originalName = fileName },
            ipAddress,
            cancellationToken);

        DeleteOldImage(uploadRoot, oldImagePath);

        logger.LogInformation(
            "Gambar produk {ProductId} diperbarui ({FileName}).",
            product.Id,
            fileNameOnDisk);

        return ServiceResult<ProductDto>.Success(MapProduct(product));
    }

    private string GetUploadRoot()
    {
        var configured = storageOptions.Value.UploadPath;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
    }

    private void DeleteOldImage(string uploadRoot, string? oldImagePath)
    {
        if (string.IsNullOrWhiteSpace(oldImagePath) ||
            !oldImagePath.StartsWith("products/", StringComparison.OrdinalIgnoreCase) ||
            oldImagePath.Contains("..", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var fullPath = Path.Combine(uploadRoot, oldImagePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Gagal menghapus gambar lama {ImagePath}.", oldImagePath);
        }
    }

    private static (string Extension, string ContentType)? DetectImageFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(PngSignature))
        {
            return (".png", "image/png");
        }

        if (header.Length >= 12 &&
            header[..4].SequenceEqual(RiffSignature) &&
            header[8..12].SequenceEqual(WebpSignature))
        {
            return (".webp", "image/webp");
        }

        return null;
    }

    private static ProductDto MapProduct(Hanora.Domain.Entities.Product product) =>
        new(
            product.Id,
            product.Name,
            product.Slug,
            product.Description,
            product.Price,
            string.IsNullOrWhiteSpace(product.ImagePath) ? null : $"/media/{product.ImagePath}",
            product.IsAvailable,
            product.ArchivedAt is not null,
            product.CategoryId,
            product.Category.Name,
            product.Category.Slug,
            product.CreatedAt,
            product.UpdatedAt);
}
