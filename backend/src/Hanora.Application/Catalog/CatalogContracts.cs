using System.ComponentModel.DataAnnotations;

namespace Hanora.Application.Catalog;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    int SortOrder,
    bool IsActive,
    int ProductCount);

public sealed class CategoryRequest
{
    [Required(ErrorMessage = "Nama kategori wajib diisi.")]
    [MinLength(1, ErrorMessage = "Nama kategori wajib diisi.")]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Slug { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsAvailable,
    bool IsArchived,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class ProductRequest
{
    [Required(ErrorMessage = "Nama produk wajib diisi.")]
    [MinLength(1, ErrorMessage = "Nama produk wajib diisi.")]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kategori wajib dipilih.")]
    public Guid CategoryId { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(1, 999_999_999, ErrorMessage = "Harga harus lebih dari 0.")]
    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public sealed class ProductAvailabilityRequest
{
    public bool IsAvailable { get; set; }
}

public sealed record SiteSettingsDto(
    string Name,
    string Tagline,
    string Description,
    string Address,
    string Phone,
    string Instagram,
    string OpeningHours,
    string MapsUrl,
    string LogoUrl);

public sealed class SiteSettingsRequest
{
    [Required(ErrorMessage = "Nama bisnis wajib diisi.")]
    [MinLength(1, ErrorMessage = "Nama bisnis wajib diisi.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Tagline { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Instagram { get; set; } = string.Empty;

    [MaxLength(100)]
    public string OpeningHours { get; set; } = string.Empty;

    [MaxLength(500)]
    public string MapsUrl { get; set; } = string.Empty;
}
