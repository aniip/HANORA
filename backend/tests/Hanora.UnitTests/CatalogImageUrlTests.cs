using Hanora.Infrastructure.Services;

namespace Hanora.UnitTests;

public sealed class CatalogImageUrlTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Kosong_MengembalikanNull(string? input, string? expected)
    {
        Assert.Equal(expected, CatalogService.BuildImageUrl(input));
    }

    [Theory]
    [InlineData("products/a.jpg", "/media/products/a.jpg")]
    [InlineData("/products/a.jpg", "/media/products/a.jpg")]
    [InlineData("products/7eb5.jpg", "/media/products/7eb5.jpg")]
    public void PathLokal_MenjadiMediaUrl(string input, string expected)
    {
        Assert.Equal(expected, CatalogService.BuildImageUrl(input));
    }

    [Theory]
    [InlineData("https://images.unsplash.com/photo-123.jpg")]
    [InlineData("https://cdn.example.com/menu/latte.jpg")]
    public void UrlAbsolut_Dipertahankan(string input)
    {
        Assert.Equal(input, CatalogService.BuildImageUrl(input));
    }
}
