using Hanora.Domain.Constants;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Hanora.IntegrationTests;

public class AuthWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string OwnerEmail = "owner@test.local";
    public const string OwnerPassword = "OwnerTestPass123!";
    public const string CashierEmail = "cashier@test.local";
    public const string CashierPassword = "CashierTestPass123!";
    public const string ForcedEmail = "forced@test.local";
    public const string ForcedPassword = "ForcedTestPass123!";
    public const string ForcedNewPassword = "ForcedTestPass456!";
    public const string TestMidtransServerKey = "test-midtrans-server-key";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("hanora_test")
        .WithUsername("hanora")
        .WithPassword("hanora_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = CreateClient();
        await SeedUsersAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Database:AutoMigrate", "true");
        builder.UseSetting("Database:Seed", "true");
        builder.UseSetting("Jwt:Issuer", "hanora-test");
        builder.UseSetting("Jwt:Audience", "hanora-test-client");
        builder.UseSetting("Jwt:SigningKey", "test-only-hanora-signing-key-0123456789abcdef");
        builder.UseSetting("Jwt:AccessMinutes", "15");
        builder.UseSetting("Jwt:RefreshDays", "7");
        builder.UseSetting(
            "Storage:UploadPath",
            Path.Combine(Path.GetTempPath(), "hanora-test-uploads"));
        builder.UseSetting("Midtrans:Mode", "simulation");
        builder.UseSetting("Midtrans:ServerKey", TestMidtransServerKey);
        builder.UseSetting("Midtrans:ClientKey", "test-client-key");
        builder.UseSetting("Midtrans:QrisEnabled", "true");
        builder.UseSetting("RateLimiting:Enabled", "false");
    }

    private async Task SeedUsersAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HanoraDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var ownerRole = await dbContext.Roles
            .SingleAsync(x => x.Name == RoleNames.Owner);
        var cashierRole = await dbContext.Roles
            .SingleAsync(x => x.Name == RoleNames.Cashier);

        var now = DateTimeOffset.UtcNow;

        await AddUserAsync(
            dbContext, passwordHasher, ownerRole,
            "Owner Test", OwnerEmail, OwnerPassword, mustChangePassword: false, now);

        await AddUserAsync(
            dbContext, passwordHasher, cashierRole,
            "Kasir Test", CashierEmail, CashierPassword, mustChangePassword: false, now);

        await AddUserAsync(
            dbContext, passwordHasher, ownerRole,
            "Owner Wajib Ganti", ForcedEmail, ForcedPassword, mustChangePassword: true, now);

        await dbContext.SaveChangesAsync();
    }

    private static async Task AddUserAsync(
        HanoraDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        Role role,
        string name,
        string email,
        string password,
        bool mustChangePassword,
        DateTimeOffset now)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail))
        {
            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            Role = role,
            Name = name,
            Email = normalizedEmail,
            MustChangePassword = mustChangePassword,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        dbContext.Users.Add(user);
    }
}
