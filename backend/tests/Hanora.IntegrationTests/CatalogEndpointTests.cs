using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Hanora.IntegrationTests;

public sealed class CatalogEndpointTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII=";

    [Fact]
    public async Task PublicSite_MengembalikanDataDasar()
    {
        using var client = factory.CreateClient();

        var site = await client.GetFromJsonAsync<SiteResponse>("/api/v1/public/site");

        Assert.NotNull(site);
        Assert.False(string.IsNullOrWhiteSpace(site.Name));
        Assert.EndsWith("hanora-logo.jpeg", site.LogoUrl);
    }

    [Fact]
    public async Task Owner_MembuatKategoriDanProduk_MunculDiPublik()
    {
        using var client = await CreateOwnerClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var category = await CreateCategoryAsync(client, $"Coffee {suffix}");
        var product = await CreateProductAsync(
            client, category.Id, $"Latte {suffix}", 32000m);

        var publicCategories = await client.GetFromJsonAsync<List<CategoryResponse>>(
            "/api/v1/public/categories");
        Assert.Contains(publicCategories!, x => x.Id == category.Id);

        var publicProducts = await client.GetFromJsonAsync<PagedProductsResponse>(
            $"/api/v1/public/products?category={category.Slug}");
        Assert.Contains(publicProducts!.Items, x => x.Id == product.Id);

        var detail = await client.GetFromJsonAsync<ProductResponse>(
            $"/api/v1/public/products/{product.Slug}");
        Assert.NotNull(detail);
        Assert.Equal(product.Name, detail.Name);
    }

    [Fact]
    public async Task ProdukTidakTersedia_TidakMunculDiPublik()
    {
        using var client = await CreateOwnerClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var category = await CreateCategoryAsync(client, $"NonCoffee {suffix}");
        var product = await CreateProductAsync(
            client, category.Id, $"Matcha {suffix}", 34000m);

        var patch = await client.PatchAsJsonAsync(
            $"/api/v1/owner/products/{product.Id}/availability",
            new { isAvailable = false });
        patch.EnsureSuccessStatusCode();

        var publicProducts = await client.GetFromJsonAsync<PagedProductsResponse>(
            $"/api/v1/public/products?category={category.Slug}");
        Assert.DoesNotContain(publicProducts!.Items, x => x.Id == product.Id);

        var ownerProducts = await client.GetFromJsonAsync<PagedProductsResponse>(
            $"/api/v1/owner/products?categoryId={category.Id}&status=unavailable");
        Assert.Contains(ownerProducts!.Items, x => x.Id == product.Id);
    }

    [Fact]
    public async Task Upload_GambarValid_DapatDiakses_Dan_FileInvalid_Ditolak()
    {
        using var client = await CreateOwnerClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var category = await CreateCategoryAsync(client, $"Pastry {suffix}");
        var product = await CreateProductAsync(
            client, category.Id, $"Croissant {suffix}", 24000m);

        using var imageContent = new MultipartFormDataContent();
        var imageBytes = Convert.FromBase64String(TinyPngBase64);
        var imagePart = new ByteArrayContent(imageBytes);
        imagePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        imageContent.Add(imagePart, "file", "produk.png");

        var upload = await client.PostAsync(
            $"/api/v1/owner/products/{product.Id}/image", imageContent);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploaded = await upload.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(uploaded);
        Assert.NotNull(uploaded.ImageUrl);
        Assert.StartsWith("/media/products/", uploaded.ImageUrl);

        var media = await client.GetAsync(uploaded.ImageUrl);
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);

        using var invalidContent = new MultipartFormDataContent();
        var invalidPart = new ByteArrayContent("bukan gambar"u8.ToArray());
        invalidPart.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        invalidContent.Add(invalidPart, "file", "produk.txt");

        var invalidUpload = await client.PostAsync(
            $"/api/v1/owner/products/{product.Id}/image", invalidContent);

        Assert.Equal(HttpStatusCode.BadRequest, invalidUpload.StatusCode);
    }

    [Fact]
    public async Task Kasir_TidakBisaMengelolaProduk()
    {
        using var client = factory.CreateClient();
        var login = await LoginAsync(
            client, AuthWebApplicationFactory.CashierEmail, AuthWebApplicationFactory.CashierPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/owner/categories", new
        {
            name = "Tidak Boleh",
            sortOrder = 1,
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Kategori_DenganProduk_TidakBisaDihapus()
    {
        using var client = await CreateOwnerClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var category = await CreateCategoryAsync(client, $"Food {suffix}");
        _ = await CreateProductAsync(client, category.Id, $"Sandwich {suffix}", 39000m);

        var response = await client.DeleteAsync($"/api/v1/owner/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task OwnerSettings_Diperbarui_TampilDiPublik()
    {
        using var client = await CreateOwnerClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var update = await client.PutAsJsonAsync("/api/v1/owner/settings", new
        {
            name = $"HANORA Test {suffix}",
            tagline = "Tagline uji",
            description = "Deskripsi uji",
            address = "Jl. Uji No. 1",
            phone = "08123456789",
            instagram = "@hanoratest",
            openingHours = "09.00-22.00",
            mapsUrl = "https://maps.google.com/?q=hanora"
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var site = await client.GetFromJsonAsync<SiteResponse>("/api/v1/public/site");
        Assert.NotNull(site);
        Assert.Equal($"HANORA Test {suffix}", site.Name);
        Assert.Equal("Jl. Uji No. 1", site.Address);

        await client.PutAsJsonAsync("/api/v1/owner/settings", new
        {
            name = "HANORA CAFE",
            tagline = "A little pause, a lot of warmth.",
            description = "Tempat singgah untuk secangkir kopi yang dibuat dengan hati.",
            address = string.Empty,
            phone = string.Empty,
            instagram = string.Empty,
            openingHours = string.Empty,
            mapsUrl = string.Empty
        });
    }

    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = factory.CreateClient();
        var login = await LoginAsync(
            client, AuthWebApplicationFactory.OwnerEmail, AuthWebApplicationFactory.OwnerPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    private static async Task<LoginResponse> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(
        HttpClient client,
        string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/owner/categories", new
        {
            name,
            sortOrder = 1,
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(category);
        return category;
    }

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient client,
        Guid categoryId,
        string name,
        decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/v1/owner/products", new
        {
            name,
            categoryId,
            description = "Produk uji integrasi.",
            price,
            isAvailable = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
        return product;
    }

    private sealed record LoginResponse(string AccessToken, int ExpiresIn, UserPayload User);

    private sealed record UserPayload(Guid Id, string Name, string Email, string Role);

    private sealed record SiteResponse(
        string Name,
        string Tagline,
        string Description,
        string Address,
        string Phone,
        string Instagram,
        string OpeningHours,
        string MapsUrl,
        string LogoUrl);

    private sealed record CategoryResponse(
        Guid Id,
        string Name,
        string Slug,
        int SortOrder,
        bool IsActive,
        int ProductCount);

    private sealed record ProductResponse(
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
        string CategorySlug);

    private sealed record PagedProductsResponse(
        IReadOnlyList<ProductResponse> Items,
        int Page,
        int PageSize,
        int TotalItems,
        int TotalPages);
}
