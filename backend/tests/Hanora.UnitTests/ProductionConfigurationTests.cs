using Hanora.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Hanora.UnitTests;

public sealed class ProductionConfigurationTests
{
    [Fact]
    public void NonProduction_TidakMemvalidasiNilaiDevelopment()
    {
        var configuration = BuildConfiguration("Host=localhost;Password=hanora_dev_password");
        var appOptions = new AppOptions { BaseUrl = "http://localhost:8080" };

        ProductionConfiguration.Validate(configuration, appOptions, isProduction: false);
    }

    [Fact]
    public void Production_MenolakBaseUrlHttp()
    {
        var configuration = BuildConfiguration(
            "Host=db;Password=strong-password",
            "https://cafe.example.com");
        var appOptions = new AppOptions { BaseUrl = "http://cafe.example.com" };

        Assert.Throws<InvalidOperationException>(
            () => ProductionConfiguration.Validate(
                configuration, appOptions, isProduction: true));
    }

    [Fact]
    public void Production_MenolakPasswordDevelopment()
    {
        var configuration = BuildConfiguration(
            "Host=db;Password=hanora_dev_password",
            "https://cafe.example.com",
            "https://cafe.example.com");
        var appOptions = new AppOptions { BaseUrl = "https://cafe.example.com" };

        Assert.Throws<InvalidOperationException>(
            () => ProductionConfiguration.Validate(
                configuration, appOptions, isProduction: true));
    }

    [Fact]
    public void Production_MenolakCorsLocalhost()
    {
        var configuration = BuildConfiguration(
            "Host=db;Password=strong-password",
            "https://cafe.example.com",
            "http://localhost:5173");
        var appOptions = new AppOptions { BaseUrl = "https://cafe.example.com" };

        Assert.Throws<InvalidOperationException>(
            () => ProductionConfiguration.Validate(
                configuration, appOptions, isProduction: true));
    }

    [Fact]
    public void Production_MenerimaKonfigurasiValid()
    {
        var configuration = BuildConfiguration(
            "Host=db;Password=strong-password",
            "https://cafe.example.com",
            "https://cafe.example.com");
        var appOptions = new AppOptions { BaseUrl = "https://cafe.example.com" };

        ProductionConfiguration.Validate(configuration, appOptions, isProduction: true);
    }

    [Fact]
    public void RateLimit_MenolakBatasTidakValid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthPermitLimit"] = "0"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => RateLimitOptions.Load(configuration));
    }

    private static IConfiguration BuildConfiguration(
        string connectionString,
        params string[] origins)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString
        };

        for (var index = 0; index < origins.Length; index++)
        {
            values[$"Cors:AllowedOrigins:{index}"] = origins[index];
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
