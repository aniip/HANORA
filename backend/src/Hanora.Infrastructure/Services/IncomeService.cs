using Hanora.Application.Abstractions;
using Hanora.Application.Common;
using Hanora.Application.Reporting;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hanora.Infrastructure.Services;

public sealed class IncomeService(
    HanoraDbContext dbContext,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IIncomeService
{
    public async Task<PagedResult<IncomeDto>> ListAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Incomes
            .AsNoTracking()
            .Include(x => x.Order)
            .Include(x => x.CreatedByUser)
            .Where(x => x.OccurredAt >= from && x.OccurredAt <= to);

        var totalItems = await query.CountAsync(cancellationToken);
        var incomes = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<IncomeDto>(
            incomes.Select(Map).ToList(),
            safePage,
            safePageSize,
            totalItems);
    }

    public async Task<IReadOnlyList<IncomeDto>> ListAllAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var incomes = await dbContext.Incomes
            .AsNoTracking()
            .Include(x => x.Order)
            .Include(x => x.CreatedByUser)
            .Where(x => x.OccurredAt >= from && x.OccurredAt <= to)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return incomes.Select(Map).ToList();
    }

    public async Task<ServiceResult<IncomeDto>> CreateManualAsync(
        ManualIncomeRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ServiceResult<IncomeDto>.Failure(
                "validation", "Data pemasukan wajib diisi.");
        }

        if (string.IsNullOrWhiteSpace(request.Description) ||
            request.Description.Trim().Length < 2)
        {
            return ServiceResult<IncomeDto>.Failure(
                "validation", "Deskripsi minimal 2 karakter.");
        }

        if (request.Amount <= 0)
        {
            return ServiceResult<IncomeDto>.Failure(
                "validation", "Nominal pemasukan harus lebih dari 0.");
        }

        var now = timeProvider.GetUtcNow();
        var income = new Income
        {
            Id = Guid.NewGuid(),
            Source = "manual",
            Description = request.Description.Trim(),
            Amount = request.Amount,
            OccurredAt = request.OccurredAt == default
                ? ReportPeriod.Today()
                : request.OccurredAt,
            CreatedBy = actorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Incomes.Add(income);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            actorId,
            "income.manual_created",
            "income",
            income.Id.ToString(),
            new { income.Description, income.Amount, income.OccurredAt },
            ipAddress,
            cancellationToken);

        var created = await dbContext.Incomes
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .SingleAsync(x => x.Id == income.Id, cancellationToken);

        return ServiceResult<IncomeDto>.Success(Map(created));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        Guid id,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var income = await dbContext.Incomes
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (income is null)
        {
            return ServiceResult<bool>.Failure(
                "not_found", "Pemasukan tidak ditemukan.");
        }

        if (!string.Equals(income.Source, "manual", StringComparison.Ordinal))
        {
            return ServiceResult<bool>.Failure(
                "income_from_order",
                "Pemasukan dari pesanan tidak dapat dihapus manual. " +
                "Gunakan proses pembatalan/refund.");
        }

        dbContext.Incomes.Remove(income);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            actorId,
            "income.manual_deleted",
            "income",
            id.ToString(),
            new { income.Description, income.Amount, income.OccurredAt },
            ipAddress,
            cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    private static IncomeDto Map(Income income) =>
        new(
            income.Id,
            income.Source,
            income.Description,
            income.Amount,
            income.OccurredAt,
            income.Order?.OrderNumber,
            income.CreatedByUser?.Name,
            income.CreatedAt);
}
