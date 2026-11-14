using Microsoft.Extensions.Configuration;

namespace Hanora.Infrastructure.Configuration;

public sealed class MidtransOptions
{
    public const string SectionName = "Midtrans";

    public string Mode { get; set; } = "simulation";
    public string ServerKey { get; set; } = string.Empty;
    public string ClientKey { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string SnapUrl { get; set; } = string.Empty;
    public bool QrisEnabled { get; set; } = true;

    public bool IsSimulation => string.Equals(Mode, "simulation", StringComparison.OrdinalIgnoreCase);
    public bool IsProduction => string.Equals(Mode, "production", StringComparison.OrdinalIgnoreCase);

    public static MidtransOptions Load(IConfiguration configuration, bool isProduction)
    {
        var options = configuration.GetSection(SectionName).Get<MidtransOptions>()
            ?? new MidtransOptions();

        options.Mode = (ReadString("MIDTRANS_MODE") ?? options.Mode).Trim().ToLowerInvariant();
        options.ServerKey = ReadString("MIDTRANS_SERVER_KEY") ?? options.ServerKey;
        options.ClientKey = ReadString("MIDTRANS_CLIENT_KEY") ?? options.ClientKey;
        options.MerchantId = ReadString("MIDTRANS_MERCHANT_ID") ?? options.MerchantId;
        options.SnapUrl = ReadString("MIDTRANS_SNAP_URL") ?? options.SnapUrl;

        var qrisEnv = ReadString("MIDTRANS_QRIS_ENABLED");
        if (bool.TryParse(qrisEnv, out var qrisEnabled))
        {
            options.QrisEnabled = qrisEnabled;
        }

        if (options.Mode is not ("simulation" or "sandbox" or "production"))
        {
            throw new InvalidOperationException(
                "Midtrans:Mode harus simulation, sandbox, atau production.");
        }

        if (isProduction && options.IsSimulation)
        {
            throw new InvalidOperationException(
                "Mode Midtrans simulation tidak boleh dipakai di production.");
        }

        if (!options.IsSimulation)
        {
            if (string.IsNullOrWhiteSpace(options.ServerKey) ||
                string.IsNullOrWhiteSpace(options.ClientKey))
            {
                throw new InvalidOperationException(
                    "MIDTRANS_SERVER_KEY dan MIDTRANS_CLIENT_KEY wajib diisi untuk mode sandbox/production.");
            }

            if (options.IsProduction &&
                options.ServerKey.StartsWith("SB-", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Server Key sandbox tidak boleh dipakai di mode production.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.SnapUrl))
        {
            options.SnapUrl = options.IsProduction
                ? "https://app.midtrans.com/snap/v1/transactions"
                : "https://app.sandbox.midtrans.com/snap/v1/transactions";
        }

        return options;
    }

    private static string? ReadString(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
