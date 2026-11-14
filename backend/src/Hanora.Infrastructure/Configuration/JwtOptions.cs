using System.Text;
using Microsoft.Extensions.Configuration;

namespace Hanora.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "hanora-api";
    public string Audience { get; set; } = "hanora-client";
    public string TableAudience { get; set; } = "hanora-table";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessMinutes { get; set; } = 15;
    public int RefreshDays { get; set; } = 7;
    public int TableSessionMinutes { get; set; } = 120;

    public static JwtOptions Load(IConfiguration configuration, bool isProduction)
    {
        var options = configuration.GetSection(SectionName).Get<JwtOptions>() ?? new JwtOptions();

        options.Issuer = ReadString("JWT_ISSUER") ?? options.Issuer;
        options.Audience = ReadString("JWT_AUDIENCE") ?? options.Audience;
        options.TableAudience = ReadString("JWT_TABLE_AUDIENCE") ?? options.TableAudience;
        options.SigningKey = ReadString("JWT_SIGNING_KEY") ?? options.SigningKey;

        if (int.TryParse(ReadString("JWT_ACCESS_MINUTES"), out var accessMinutes))
        {
            options.AccessMinutes = accessMinutes;
        }

        if (int.TryParse(ReadString("REFRESH_TOKEN_DAYS"), out var refreshDays))
        {
            options.RefreshDays = refreshDays;
        }

        if (int.TryParse(ReadString("TABLE_SESSION_MINUTES"), out var tableSessionMinutes))
        {
            options.TableSessionMinutes = tableSessionMinutes;
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            string.IsNullOrWhiteSpace(options.TableAudience))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer, Jwt:Audience, dan Jwt:TableAudience wajib diisi.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey belum dikonfigurasi. Isi Jwt:SigningKey atau environment JWT_SIGNING_KEY.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey minimal 32 byte. Gunakan nilai acak yang panjang.");
        }

        if (isProduction &&
            options.SigningKey.StartsWith("dev-only", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey development tidak boleh dipakai di production.");
        }

        if (options.AccessMinutes is < 5 or > 120)
        {
            throw new InvalidOperationException("Jwt:AccessMinutes harus antara 5 dan 120 menit.");
        }

        if (options.RefreshDays is < 1 or > 30)
        {
            throw new InvalidOperationException("Jwt:RefreshDays harus antara 1 dan 30 hari.");
        }

        if (options.TableSessionMinutes is < 5 or > 480)
        {
            throw new InvalidOperationException("Jwt:TableSessionMinutes harus antara 5 dan 480 menit.");
        }

        return options;
    }

    private static string? ReadString(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
