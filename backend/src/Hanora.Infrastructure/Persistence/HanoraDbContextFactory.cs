using Hanora.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hanora.Infrastructure.Persistence;

/// <summary>
/// Dipakai oleh `dotnet ef` agar tidak perlu menjalankan host API.
/// </summary>
public sealed class HanoraDbContextFactory : IDesignTimeDbContextFactory<HanoraDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=hanora;Username=hanora;Password=hanora_dev_password;Maximum Pool Size=20";

    public HanoraDbContext CreateDbContext(string[] args)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var connectionString = string.IsNullOrWhiteSpace(databaseUrl)
            ? DefaultConnectionString
            : DatabaseConnection.FromUrl(databaseUrl);

        var options = new DbContextOptionsBuilder<HanoraDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(HanoraDbContext).Assembly.GetName().Name);
                NpgsqlConfiguration.MapEnums(npgsql);
            })
            .Options;

        return new HanoraDbContext(options);
    }
}
