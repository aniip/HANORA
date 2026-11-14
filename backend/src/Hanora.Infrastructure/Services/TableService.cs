using System.Security.Cryptography;
using Hanora.Application.Abstractions;
using Hanora.Application.Common;
using Hanora.Application.Tables;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Configuration;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Hanora.Infrastructure.Services;

public sealed class TableService(
    HanoraDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IAuditWriter auditWriter,
    AppOptions appOptions,
    TimeProvider timeProvider) : ITableService
{
    public async Task<IReadOnlyList<TableDto>> ListAsync(CancellationToken cancellationToken)
    {
        var tables = await dbContext.DiningTables
            .AsNoTracking()
            .Include(x => x.QrCodes)
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);

        return tables.Select(MapTable).ToList();
    }

    public async Task<ServiceResult<TableDto>> CreateAsync(
        TableRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var number = request.Number.Trim();
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<TableDto>.Failure(
                "validation", "Nomor dan nama meja wajib diisi.");
        }

        if (await dbContext.DiningTables.AnyAsync(x => x.Number == number, cancellationToken))
        {
            return ServiceResult<TableDto>.Failure(
                "duplicate_table", "Nomor meja sudah digunakan.");
        }

        var now = timeProvider.GetUtcNow();
        var table = new DiningTable
        {
            Id = Guid.NewGuid(),
            Number = number,
            Name = name,
            Capacity = request.Capacity,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        var qrCode = CreateQrCode(table.Id, now);
        table.QrCodes.Add(qrCode);
        dbContext.DiningTables.Add(table);

        await auditWriter.WriteAsync(
            actorId,
            "table.created",
            "dining_table",
            table.Id.ToString(),
            new { table.Number, table.Name },
            ipAddress,
            cancellationToken);

        return ServiceResult<TableDto>.Success(MapTable(table));
    }

    public async Task<ServiceResult<TableDto>> UpdateAsync(
        Guid tableId,
        TableRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var table = await dbContext.DiningTables
            .Include(x => x.QrCodes)
            .FirstOrDefaultAsync(x => x.Id == tableId, cancellationToken);

        if (table is null)
        {
            return ServiceResult<TableDto>.Failure(
                "not_found", "Meja tidak ditemukan.");
        }

        var number = request.Number.Trim();
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<TableDto>.Failure(
                "validation", "Nomor dan nama meja wajib diisi.");
        }

        if (await dbContext.DiningTables.AnyAsync(
                x => x.Number == number && x.Id != tableId,
                cancellationToken))
        {
            return ServiceResult<TableDto>.Failure(
                "duplicate_table", "Nomor meja sudah digunakan.");
        }

        table.Number = number;
        table.Name = name;
        table.Capacity = request.Capacity;
        table.IsActive = request.IsActive;
        table.UpdatedAt = timeProvider.GetUtcNow();

        await auditWriter.WriteAsync(
            actorId,
            "table.updated",
            "dining_table",
            table.Id.ToString(),
            new { table.Number, table.Name, table.IsActive },
            ipAddress,
            cancellationToken);

        return ServiceResult<TableDto>.Success(MapTable(table));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        Guid tableId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var table = await dbContext.DiningTables
            .Include(x => x.QrCodes)
            .FirstOrDefaultAsync(x => x.Id == tableId, cancellationToken);

        if (table is null)
        {
            return ServiceResult<bool>.Failure(
                "not_found", "Meja tidak ditemukan.");
        }

        if (await dbContext.Orders.AnyAsync(x => x.TableId == tableId, cancellationToken))
        {
            return ServiceResult<bool>.Failure(
                "table_in_use",
                "Meja memiliki riwayat pesanan. Nonaktifkan meja alih-alih menghapusnya.");
        }

        dbContext.TableQrCodes.RemoveRange(table.QrCodes);
        dbContext.DiningTables.Remove(table);

        await auditWriter.WriteAsync(
            actorId,
            "table.deleted",
            "dining_table",
            table.Id.ToString(),
            new { table.Number, table.Name },
            ipAddress,
            cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<TableQrFile>> GetQrAsync(
        Guid tableId,
        string? format,
        CancellationToken cancellationToken)
    {
        var table = await dbContext.DiningTables
            .AsNoTracking()
            .Include(x => x.QrCodes)
            .FirstOrDefaultAsync(x => x.Id == tableId, cancellationToken);

        if (table is null)
        {
            return ServiceResult<TableQrFile>.Failure(
                "not_found", "Meja tidak ditemukan.");
        }

        var activeQr = table.QrCodes
            .FirstOrDefault(x => x.IsActive && x.RevokedAt == null);

        if (activeQr is null)
        {
            return ServiceResult<TableQrFile>.Failure(
                "qr_not_found", "QR aktif untuk meja ini tidak ditemukan.");
        }

        var url = BuildQrUrl(activeQr.Token);
        var normalizedFormat = format?.Trim().ToLowerInvariant() ?? "png";

        try
        {
            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);

            if (normalizedFormat == "svg")
            {
                var svg = new SvgQRCode(qrData).GetGraphic(20);
                return ServiceResult<TableQrFile>.Success(
                    new TableQrFile(
                        System.Text.Encoding.UTF8.GetBytes(svg),
                        "image/svg+xml",
                        $"meja-{table.Number}.svg"));
            }

            var png = new PngByteQRCode(qrData).GetGraphic(20);
            return ServiceResult<TableQrFile>.Success(
                new TableQrFile(png, "image/png", $"meja-{table.Number}.png"));
        }
        catch (Exception exception)
        {
            return ServiceResult<TableQrFile>.Failure(
                "qr_error", $"Gagal membuat QR: {exception.Message}");
        }
    }

    public async Task<ServiceResult<TableDto>> RegenerateQrAsync(
        Guid tableId,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var table = await dbContext.DiningTables
            .FirstOrDefaultAsync(x => x.Id == tableId, cancellationToken);

        if (table is null)
        {
            return ServiceResult<TableDto>.Failure(
                "not_found", "Meja tidak ditemukan.");
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Bulk update menghindari urutan UPDATE/INSERT yang salah pada partial
        // unique index "satu QR aktif per meja".
        await dbContext.TableQrCodes
            .Where(x => x.TableId == tableId && x.IsActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.RevokedAt, (DateTimeOffset?)now),
                cancellationToken);

        var qrCode = CreateQrCode(table.Id, now);
        dbContext.TableQrCodes.Add(qrCode);
        table.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditWriter.WriteAsync(
            actorId,
            "table.qr_regenerated",
            "dining_table",
            table.Id.ToString(),
            new { table.Number },
            ipAddress,
            cancellationToken);

        var updated = await dbContext.DiningTables
            .AsNoTracking()
            .Include(x => x.QrCodes)
            .FirstAsync(x => x.Id == tableId, cancellationToken);

        return ServiceResult<TableDto>.Success(MapTable(updated));
    }

    public async Task<TableSessionDto?> ResolveSessionAsync(
        string qrToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return null;
        }

        var qrCode = await dbContext.TableQrCodes
            .AsNoTracking()
            .Include(x => x.Table)
            .FirstOrDefaultAsync(x =>
                x.Token == qrToken &&
                x.IsActive &&
                x.RevokedAt == null &&
                x.Table.IsActive,
                cancellationToken);

        if (qrCode is null)
        {
            return null;
        }

        var session = jwtTokenService.CreateTableSession(qrCode.Table, qrCode);

        return new TableSessionDto(
            new TableInfoDto(
                qrCode.Table.Id,
                qrCode.Table.Number,
                qrCode.Table.Name,
                qrCode.Table.Capacity),
            session.Token,
            session.ExpiresIn);
    }

    private TableQrCode CreateQrCode(Guid tableId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TableId = tableId,
            Token = GenerateToken(),
            IsActive = true,
            CreatedAt = now
        };

    private string BuildQrUrl(string token) => $"{appOptions.BaseUrl}/t/{token}";

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private TableDto MapTable(DiningTable table)
    {
        var activeQr = table.QrCodes
            .FirstOrDefault(x => x.IsActive && x.RevokedAt == null);

        return new TableDto(
            table.Id,
            table.Number,
            table.Name,
            table.Capacity,
            table.IsActive,
            activeQr is null ? null : BuildQrUrl(activeQr.Token),
            activeQr?.CreatedAt);
    }
}
