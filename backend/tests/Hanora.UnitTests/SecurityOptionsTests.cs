using Hanora.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Hanora.UnitTests;

public sealed class SecurityOptionsTests
{
    [Fact]
    public void Midtrans_Production_MenolakModeSimulation()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Midtrans:Mode"] = "simulation"
        });

        Assert.Throws<InvalidOperationException>(
            () => MidtransOptions.Load(configuration, isProduction: true));
    }

    [Fact]
    public void Midtrans_Sandbox_MembutuhkanKredensial()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Midtrans:Mode"] = "sandbox"
        });

        Assert.Throws<InvalidOperationException>(
            () => MidtransOptions.Load(configuration, isProduction: false));
    }

    [Fact]
    public void Midtrans_Production_MenolakServerKeySandbox()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Midtrans:Mode"] = "production",
            ["Midtrans:ServerKey"] = "SB-Mid-server-test",
            ["Midtrans:ClientKey"] = "SB-Mid-client-test"
        });

        Assert.Throws<InvalidOperationException>(
            () => MidtransOptions.Load(configuration, isProduction: true));
    }

    [Fact]
    public void Midtrans_Production_MenerimaKredensialValid()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Midtrans:Mode"] = "production",
            ["Midtrans:ServerKey"] = "Mid-server-production",
            ["Midtrans:ClientKey"] = "Mid-client-production"
        });

        var options = MidtransOptions.Load(configuration, isProduction: true);

        Assert.True(options.IsProduction);
        Assert.Equal(
            "https://app.midtrans.com/snap/v1/transactions",
            options.SnapUrl);
    }

    [Fact]
    public void Jwt_Production_MenolakSigningKeyDevelopment()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "hanora-api",
            ["Jwt:Audience"] = "hanora-client",
            ["Jwt:TableAudience"] = "hanora-table",
            ["Jwt:SigningKey"] = "dev-only-hanora-signing-key-change-me-0123456789abcdef"
        });

        Assert.Throws<InvalidOperationException>(
            () => JwtOptions.Load(configuration, isProduction: true));
    }

    [Fact]
    public void Jwt_Production_MenerimaSigningKeyKuat()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "hanora-api",
            ["Jwt:Audience"] = "hanora-client",
            ["Jwt:TableAudience"] = "hanora-table",
            ["Jwt:SigningKey"] = "production-only-signing-key-0123456789abcdefghijklmnop"
        });

        var options = JwtOptions.Load(configuration, isProduction: true);

        Assert.Equal("hanora-api", options.Issuer);
    }

    private static IConfiguration BuildConfiguration(
        Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
