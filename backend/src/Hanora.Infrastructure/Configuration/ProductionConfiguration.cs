using Microsoft.Extensions.Configuration;

namespace Hanora.Infrastructure.Configuration;

/// <summary>
/// Validasi konfigurasi yang hanya relevan untuk production. Tujuannya agar
/// kesalahan konfigurasi terdeteksi saat startup, bukan setelah go-live.
/// </summary>
public static class ProductionConfiguration
{
    private static readonly string[] DevelopmentPasswordMarkers =
    [
        "hanora_dev_password",
        "hanora_test_password",
        "ganti-dengan-password"
    ];

    public static void Validate(
        IConfiguration configuration,
        AppOptions appOptions,
        bool isProduction)
    {
        if (!isProduction)
        {
            return;
        }

        if (!appOptions.BaseUrl.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "App:BaseUrl wajib menggunakan HTTPS di production.");
        }

        var connectionString = DatabaseConnection.Resolve(configuration);

        foreach (var marker in DevelopmentPasswordMarkers)
        {
            if (connectionString.Contains(
                    $"Password={marker}",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Password database development tidak boleh dipakai di production.");
            }
        }

        var origins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host is "127.0.0.1" or "::1")
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins berisi origin tidak aman: {origin}.");
            }
        }

    }
}
