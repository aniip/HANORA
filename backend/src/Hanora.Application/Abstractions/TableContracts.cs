using Hanora.Application.Common;
using Hanora.Application.Tables;

namespace Hanora.Application.Abstractions;

public interface ITableService
{
    Task<IReadOnlyList<TableDto>> ListAsync(CancellationToken cancellationToken);

    Task<ServiceResult<TableDto>> CreateAsync(
        TableRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<TableDto>> UpdateAsync(
        Guid tableId,
        TableRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteAsync(
        Guid tableId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<TableQrFile>> GetQrAsync(
        Guid tableId,
        string? format,
        CancellationToken cancellationToken);

    Task<ServiceResult<TableDto>> RegenerateQrAsync(
        Guid tableId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<TableSessionDto?> ResolveSessionAsync(
        string qrToken,
        CancellationToken cancellationToken);
}
