using System.Text.RegularExpressions;
using Hanora.Domain.Entities;
using Hanora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

namespace Hanora.Infrastructure.Persistence;

public sealed class HanoraDbContext(DbContextOptions<HanoraDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<TableQrCode> TableQrCodes => Set<TableQrCode>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentNotification> PaymentNotifications => Set<PaymentNotification>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<TransactionHistory> TransactionHistories => Set<TransactionHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<DailyCounter> DailyCounters => Set<DailyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");

        var translator = new NpgsqlSnakeCaseNameTranslator();
        modelBuilder.HasPostgresEnum<OrderStatus>("public", "order_status", translator);
        modelBuilder.HasPostgresEnum<PaymentStatus>("public", "payment_status", translator);
        modelBuilder.HasPostgresEnum<PaymentMethod>("public", "payment_method", translator);
        modelBuilder.HasPostgresEnum<PaymentProvider>("public", "payment_provider", translator);
        modelBuilder.HasPostgresEnum<ActorType>("public", "actor_type", translator);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HanoraDbContext).Assembly);
        ApplySnakeCaseNames(modelBuilder);
    }

    /// <summary>
    /// Menerapkan penamaan snake_case pada tabel dan kolom agar sesuai DATABASE.md.
    /// Constraint/index yang sudah memiliki nama eksplisit juga dinormalkan.
    /// </summary>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                var name = key.GetName();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    key.SetName(ToSnakeCase(name));
                }
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var name = foreignKey.GetConstraintName();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    foreignKey.SetConstraintName(ToSnakeCase(name));
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                var name = index.GetDatabaseName();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    index.SetDatabaseName(ToSnakeCase(name));
                }
            }
        }
    }

    private static string ToSnakeCase(string value) =>
        Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
}
