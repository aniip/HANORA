using Microsoft.Extensions.Configuration;

namespace Hanora.Infrastructure.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    public string BaseUrl { get; set; } = "http://localhost:8080";

    public static AppOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<AppOptions>()
            ?? new AppOptions();

        var environmentValue = Environment.GetEnvironmentVariable("APP_BASE_URL");
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            options.BaseUrl = environmentValue;
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "App:BaseUrl harus berupa URL http atau https yang valid.");
        }

        options.BaseUrl = options.BaseUrl.TrimEnd('/');
        return options;
    }
}
