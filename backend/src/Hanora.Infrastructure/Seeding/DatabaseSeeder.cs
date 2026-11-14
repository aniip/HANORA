using System.Text.Json;
using Hanora.Domain.Constants;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hanora.Infrastructure.Seeding;

/// <summary>
/// Seed minimal: role, pengaturan default, dan satu akun owner dari environment.
/// Tidak ada produk, meja, pesanan, atau transaksi contoh.
/// </summary>
public sealed class DatabaseSeeder(
    HanoraDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var ownerRole = await EnsureRoleAsync(
            RoleNames.Owner,
            "Akses penuh ke seluruh sistem.",
            cancellationToken);
        await EnsureRoleAsync(
            RoleNames.Cashier,
            "Operasional kasir dan konfirmasi pembayaran tunai.",
            cancellationToken);
        await EnsureRoleAsync(
            RoleNames.Customer,
            "Principal sesi meja untuk customer guest.",
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedOwnerAsync(ownerRole, cancellationToken);
    }

    private async Task<Role> EnsureRoleAsync(
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        if (role is not null)
        {
            return role;
        }

        role = new Role
        {
            Name = name,
            Description = description,
            CreatedAt = timeProvider.GetUtcNow()
        };

        dbContext.Roles.Add(role);
        logger.LogInformation("Role {Role} dibuat.", name);
        return role;
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var defaults = new Dictionary<string, object?>
        {
            ["tax_rate"] = 10.00m,
            ["payment_expiry_minutes"] = 15,
            ["order_deadline_minutes"] = 30,
            ["business.name"] = "HANORA CAFE",
            ["business.tagline"] = "A little pause, a lot of warmth.",
            ["business.description"] =
                "Tempat singgah untuk secangkir kopi yang dibuat dengan hati. " +
                "Nikmati rasa, ruang, dan cerita dalam setiap kunjungan.",
            ["business.address"] = string.Empty,
            ["business.phone"] = string.Empty,
            ["business.instagram"] = string.Empty,
            ["business.opening_hours"] = string.Empty,
            ["business.maps_url"] = string.Empty
        };

        var existingKeys = await dbContext.Settings
            .Select(x => x.Key)
            .ToListAsync(cancellationToken);

        foreach (var (key, value) in defaults)
        {
            if (existingKeys.Contains(key))
            {
                continue;
            }

            dbContext.Settings.Add(new Setting
            {
                Key = key,
                Value = JsonSerializer.Serialize(value),
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedOwnerAsync(Role ownerRole, CancellationToken cancellationToken)
    {
        var email = ReadSetting("SEED_OWNER_EMAIL", "Seed:OwnerEmail");
        var password = ReadSetting("SEED_OWNER_PASSWORD", "Seed:OwnerPassword");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "SEED_OWNER_EMAIL/SEED_OWNER_PASSWORD tidak diisi. " +
                "Akun owner awal tidak dibuat.");
            return;
        }

        if (password.Length < 12)
        {
            logger.LogWarning(
                "SEED_OWNER_PASSWORD kurang dari 12 karakter. " +
                "Akun owner awal tidak dibuat.");
            return;
        }

        var ownerExists = await dbContext.Users
            .AnyAsync(x => x.RoleId == ownerRole.Id, cancellationToken);

        if (ownerExists)
        {
            logger.LogInformation("Akun owner sudah ada. Seed owner dilewati.");
            return;
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = ownerRole.Id,
            Role = ownerRole,
            Name = "Owner Hanora",
            Email = email.Trim().ToLowerInvariant(),
            MustChangePassword = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Akun owner awal dibuat untuk {Email}.", user.Email);
    }

    private string? ReadSetting(string environmentName, string configurationKey)
    {
        var environmentValue = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        var configurationValue = configuration[configurationKey];
        return string.IsNullOrWhiteSpace(configurationValue) ? null : configurationValue;
    }
}
