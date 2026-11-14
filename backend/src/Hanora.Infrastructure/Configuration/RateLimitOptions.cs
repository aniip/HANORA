using Microsoft.Extensions.Configuration;

namespace Hanora.Infrastructure.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;
    public int AuthPermitLimit { get; set; } = 10;
    public int PublicPermitLimit { get; set; } = 120;
    public int OrderPermitLimit { get; set; } = 40;
    public int WebhookPermitLimit { get; set; } = 120;
    public int StaffPermitLimit { get; set; } = 300;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; }

    public static RateLimitOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        var enabled = Environment.GetEnvironmentVariable("RATE_LIMIT_ENABLED");
        if (bool.TryParse(enabled, out var parsedEnabled))
        {
            options.Enabled = parsedEnabled;
        }

        Validate(options);
        return options;
    }

    private static void Validate(RateLimitOptions options)
    {
        if (options.AuthPermitLimit < 1 ||
            options.PublicPermitLimit < 1 ||
            options.OrderPermitLimit < 1 ||
            options.WebhookPermitLimit < 1 ||
            options.StaffPermitLimit < 1)
        {
            throw new InvalidOperationException(
                "RateLimiting: semua PermitLimit harus lebih dari 0.");
        }

        if (options.WindowSeconds is < 10 or > 3600)
        {
            throw new InvalidOperationException(
                "RateLimiting:WindowSeconds harus antara 10 dan 3600 detik.");
        }

        if (options.QueueLimit is < 0 or > 1000)
        {
            throw new InvalidOperationException(
                "RateLimiting:QueueLimit harus antara 0 dan 1000.");
        }
    }
}
