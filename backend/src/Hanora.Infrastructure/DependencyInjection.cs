using Hanora.Application.Abstractions;
using Hanora.Application.Payments;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Configuration;
using Hanora.Infrastructure.Persistence;
using Hanora.Infrastructure.Seeding;
using Hanora.Infrastructure.Services;
using Hanora.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hanora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseConnection.Resolve(configuration);

        // Opsi DbContext dibuat sekali agar Npgsql memakai satu connection pool.
        // Jika opsi dibuat per scope, setiap request dapat membuat pool baru dan
        // menghabiskan max_connections PostgreSQL.
        var optionsBuilder = new DbContextOptionsBuilder<HanoraDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(
                    typeof(HanoraDbContext).Assembly.GetName().Name);
                NpgsqlConfiguration.MapEnums(npgsql);
            })
            .ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        var dbContextOptions = optionsBuilder.Options;
        services.AddSingleton(dbContextOptions);
        services.AddScoped<HanoraDbContext>();

        services.Configure<StorageOptions>(
            configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IStorageProbe, LocalStorageProbe>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IProductImageService, ProductImageService>();
        services.AddScoped<ITableService, TableService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddHttpClient<IMidtransClient, MidtransClient>(
            client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IIncomeService, IncomeService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
