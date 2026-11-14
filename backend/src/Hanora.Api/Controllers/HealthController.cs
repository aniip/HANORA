using Hanora.Infrastructure.Persistence;
using Hanora.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hanora.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(
    HanoraDbContext dbContext,
    IStorageProbe storageProbe,
    IHostEnvironment environment,
    ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "hanora-api",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            environment = environment.EnvironmentName,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var databaseUp = await CanConnectToDatabaseAsync(cancellationToken);
        var storage = await storageProbe.CheckAsync(cancellationToken);
        var ready = databaseUp && storage.Writable;

        var payload = new
        {
            status = ready ? "ready" : "not_ready",
            database = databaseUp ? "up" : "down",
            storage = storage.Writable ? "writable" : "unavailable",
            timestamp = DateTimeOffset.UtcNow
        };

        return ready
            ? Ok(payload)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
    }

    private async Task<bool> CanConnectToDatabaseAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            return await dbContext.Database.CanConnectAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Readiness check database melewati batas waktu.");
            return false;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Readiness check database gagal.");
            return false;
        }
    }
}
