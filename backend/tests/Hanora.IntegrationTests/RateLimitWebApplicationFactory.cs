using Microsoft.AspNetCore.Hosting;

namespace Hanora.IntegrationTests;

public sealed class RateLimitWebApplicationFactory : AuthWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "3");
        builder.UseSetting("RateLimiting:PublicPermitLimit", "1000");
        builder.UseSetting("RateLimiting:OrderPermitLimit", "1000");
        builder.UseSetting("RateLimiting:WebhookPermitLimit", "1000");
        builder.UseSetting("RateLimiting:StaffPermitLimit", "1000");
        builder.UseSetting("RateLimiting:WindowSeconds", "60");
    }
}
