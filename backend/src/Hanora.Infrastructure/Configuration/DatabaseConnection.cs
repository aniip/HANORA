using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Hanora.Infrastructure.Configuration;

/// <summary>
/// Membaca connection string dari konfigurasi .NET atau environment DATABASE_URL.
/// Format DATABASE_URL yang didukung: postgres://user:password@host:port/database
/// </summary>
public static class DatabaseConnection
{
    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return FromUrl(databaseUrl);
        }

        throw new InvalidOperationException(
            "Connection string database belum dikonfigurasi. " +
            "Isi ConnectionStrings:Default atau environment variable DATABASE_URL.");
    }

    public static string FromUrl(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            throw new InvalidOperationException(
                "DATABASE_URL harus menggunakan format postgres://user:password@host:port/database.");
        }

        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("DATABASE_URL tidak menyertakan nama database.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        if (string.IsNullOrWhiteSpace(userInfo[0]))
        {
            throw new InvalidOperationException("DATABASE_URL tidak menyertakan username.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = SslMode.Prefer
        };

        return builder.ConnectionString;
    }
}
