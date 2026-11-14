using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Hanora.Api.Security;
using Hanora.Application.Auth;
using Hanora.Application.Security;
using Hanora.Domain.Constants;
using Hanora.Infrastructure;
using Hanora.Infrastructure.Configuration;
using Hanora.Infrastructure.Persistence;
using Hanora.Infrastructure.Seeding;
using Hanora.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    // Cukup untuk JSON dan upload gambar 2 MB; mencegah body raksasa.
    options.Limits.MaxRequestBodySize = 6 * 1024 * 1024;
});

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var jwtOptions = JwtOptions.Load(
    builder.Configuration,
    builder.Environment.IsProduction());

var appOptions = AppOptions.Load(builder.Configuration);

// Mode simulation ditolak saat production agar pembayaran palsu tidak
// pernah aktif di lingkungan nyata.
var midtransOptions = MidtransOptions.Load(
    builder.Configuration,
    builder.Environment.IsProduction());

var rateLimitOptions = RateLimitOptions.Load(builder.Configuration);

ProductionConfiguration.Validate(
    builder.Configuration,
    appOptions,
    builder.Environment.IsProduction());

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(appOptions);
builder.Services.AddSingleton(midtransOptions);
builder.Services.AddSingleton(rateLimitOptions);

// Kunci Data Protection dipersistenkan agar cookie tetap valid setelah
// container restart. Di development path boleh kosong (kunci di memori).
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("Hanora");

var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    var keysDirectory = Path.IsPathRooted(keysPath)
        ? keysPath
        : Path.Combine(builder.Environment.ContentRootPath, keysPath);

    Directory.CreateDirectory(keysDirectory);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // API hanya dijangkau melalui reverse proxy di jaringan internal.
    // Hapus daftar known proxy agar IP asli dari Caddy/nginx dipakai untuk
    // audit log dan rate limiting.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudiences = [jwtOptions.Audience, jwtOptions.TableAudience],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Tidak terautentikasi",
                    Detail = "Token tidak ada atau tidak valid."
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Akses ditolak",
                    Detail = "Anda tidak memiliki izin untuk mengakses sumber daya ini."
                });
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.OwnerOnly, policy =>
        policy.RequireRole(RoleNames.Owner));

    options.AddPolicy(Policies.OwnerOrCashier, policy =>
        policy.RequireRole(RoleNames.Owner, RoleNames.Cashier));

    options.AddPolicy(Policies.TableSession, policy =>
        policy
            .RequireRole(RoleNames.Customer)
            .RequireClaim(AuthConstants.TableIdClaim));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.WithOrigins("http://localhost:5173");
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        policy.AllowCredentials();
    });
});

if (rateLimitOptions.Enabled)
{
    var window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds);

    static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    static RateLimitPartition<string> Partition(
        HttpContext context,
        int permitLimit,
        TimeSpan window,
        int queueLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = queueLimit,
                AutoReplenishment = true
            });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode =
                StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType =
                "application/problem+json";
            context.HttpContext.Response.Headers.RetryAfter =
                rateLimitOptions.WindowSeconds.ToString();

            await context.HttpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Terlalu banyak permintaan",
                    Detail = "Silakan coba lagi beberapa saat lagi."
                },
                cancellationToken);
        };

        options.GlobalLimiter =
            PartitionedRateLimiter.Create<HttpContext, string>(context =>
                Partition(
                    context,
                    rateLimitOptions.StaffPermitLimit,
                    window,
                    rateLimitOptions.QueueLimit));

        options.AddPolicy(
            RateLimitPolicies.Auth,
            context => Partition(
                context,
                rateLimitOptions.AuthPermitLimit,
                window,
                rateLimitOptions.QueueLimit));

        options.AddPolicy(
            RateLimitPolicies.Public,
            context => Partition(
                context,
                rateLimitOptions.PublicPermitLimit,
                window,
                rateLimitOptions.QueueLimit));

        options.AddPolicy(
            RateLimitPolicies.Orders,
            context => Partition(
                context,
                rateLimitOptions.OrderPermitLimit,
                window,
                rateLimitOptions.QueueLimit));

        options.AddPolicy(
            RateLimitPolicies.Webhook,
            context => Partition(
                context,
                rateLimitOptions.WebhookPermitLimit,
                window,
                rateLimitOptions.QueueLimit));
    });
}

var app = builder.Build();

if (args.Any(x => string.Equals(
        x,
        "--migrate",
        StringComparison.OrdinalIgnoreCase)))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider
        .GetRequiredService<HanoraDbContext>();

    await migrationDb.Database.MigrateAsync();

    if (args.Any(x => string.Equals(
            x,
            "--seed",
            StringComparison.OrdinalIgnoreCase)))
    {
        await migrationScope.ServiceProvider
            .GetRequiredService<DatabaseSeeder>()
            .SeedAsync();
    }

    app.Logger.LogInformation("Migrasi database selesai.");
    return;
}

var autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrate");
var seedDatabase = app.Configuration.GetValue<bool>("Database:Seed");

if (autoMigrate || seedDatabase)
{
    using var scope = app.Services.CreateScope();

    try
    {
        if (autoMigrate)
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HanoraDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        if (seedDatabase)
        {
            await scope.ServiceProvider
                .GetRequiredService<DatabaseSeeder>()
                .SeedAsync();
        }
    }
    catch (Exception exception)
    {
        app.Logger.LogError(
            exception,
            "Migrasi/seed database gagal. API tetap berjalan; readiness akan melaporkan database down.");
    }
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Header keamanan untuk semua respons API. CSP untuk halaman SPA diatur nginx.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Cross-Origin-Resource-Policy"] = "same-origin";

    if (context.Request.IsHttps)
    {
        headers["Strict-Transport-Security"] =
            "max-age=31536000; includeSubDomains";
    }

    await next();
});

var storageOptions = app.Configuration
    .GetSection(StorageOptions.SectionName)
    .Get<StorageOptions>() ?? new StorageOptions();

var uploadRoot = Path.IsPathRooted(storageOptions.UploadPath)
    ? storageOptions.UploadPath
    : Path.Combine(app.Environment.ContentRootPath, storageOptions.UploadPath);

Directory.CreateDirectory(uploadRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadRoot),
    RequestPath = "/media",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["Cache-Control"] = "public,max-age=604800";
    }
});

app.UseCors("Frontend");

if (rateLimitOptions.Enabled)
{
    app.UseRateLimiter();
}

app.UseAuthentication();

// Pengguna staf yang wajib mengganti password hanya boleh mengakses endpoint auth.
app.Use(async (context, next) =>
{
    var isStaffPath =
        context.Request.Path.StartsWithSegments("/api/v1/owner") ||
        context.Request.Path.StartsWithSegments("/api/v1/cashier");

    var mustChangePassword =
        context.User.Identity?.IsAuthenticated == true &&
        context.User.HasClaim(AuthConstants.MustChangePasswordClaim, "true");

    if (isStaffPath && mustChangePassword)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Ganti password diperlukan",
            Detail = "Anda harus mengganti password sebelum mengakses dashboard."
        });
        return;
    }

    await next();
});

app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    name = "HANORA CAFE API",
    version = "1.0.0",
    documentation = app.Environment.IsDevelopment() ? "/scalar/v1" : null
})).ExcludeFromDescription();

app.Run();

// Diperlukan oleh integration test (WebApplicationFactory).
public partial class Program { }
