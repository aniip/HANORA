using System.Net;
using System.Text.Json;
using Hanora.Application.Abstractions;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;

namespace Hanora.Infrastructure.Services;

public sealed class AuditWriter(HanoraDbContext dbContext, TimeProvider timeProvider) : IAuditWriter
{
    public async Task WriteAsync(
        Guid? userId,
        string action,
        string entity,
        string? entityId,
        object? metadata,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
            Ip = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
            CreatedAt = timeProvider.GetUtcNow()
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
