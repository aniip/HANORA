using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hanora.Infrastructure.Storage;

public sealed record StorageStatus(bool Writable, string Path);

public interface IStorageProbe
{
    Task<StorageStatus> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Memastikan direktori upload dapat ditulis. Dipakai oleh readiness check.
/// </summary>
public sealed class LocalStorageProbe(
    IOptions<StorageOptions> options,
    IHostEnvironment environment) : IStorageProbe
{
    public Task<StorageStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        var configuredPath = options.Value.UploadPath;
        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);

        try
        {
            Directory.CreateDirectory(path);
            var probeFile = Path.Combine(path, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);
            return Task.FromResult(new StorageStatus(true, path));
        }
        catch
        {
            return Task.FromResult(new StorageStatus(false, path));
        }
    }
}
