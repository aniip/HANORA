using Hanora.Infrastructure.Configuration;
using Npgsql;

namespace Hanora.UnitTests;

public sealed class DatabaseConnectionTests
{
    [Fact]
    public void FromUrl_MemecahUriPostgres()
    {
        var connectionString = DatabaseConnection.FromUrl(
            "postgres://hanora:rahasia@localhost:5433/hanora_db");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5433, builder.Port);
        Assert.Equal("hanora_db", builder.Database);
        Assert.Equal("hanora", builder.Username);
        Assert.Equal("rahasia", builder.Password);
    }

    [Fact]
    public void FromUrl_MemakaiPortDefault()
    {
        var connectionString = DatabaseConnection.FromUrl(
            "postgresql://hanora:rahasia@db/hanora");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal(5432, builder.Port);
    }

    [Fact]
    public void FromUrl_MenolakSkemaSelainPostgres()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnection.FromUrl("mysql://hanora:rahasia@localhost/hanora"));
    }

    [Fact]
    public void FromUrl_MenolakTanpaNamaDatabase()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnection.FromUrl("postgres://hanora:rahasia@localhost:5432/"));
    }
}
