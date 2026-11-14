using Hanora.Application.Abstractions;
using Hanora.Application.Common;
using Hanora.Application.Reporting;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hanora.Infrastructure.Services;

public sealed class ExpenseService(
    HanoraDbContext dbContext,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IExpenseService
{
    public async Task<PagedResult<ExpenseDto>> ListAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Expenses
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Where(x => x.OccurredAt >= from && x.OccurredAt <= to);

        var totalItems = await query.CountAsync(cancellationToken);
        var expenses = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ExpenseDto>(
            expenses.Select(Map).ToList(),
            safePage,
            safePageSize,
            totalItems);
    }

    public async Task<IReadOnlyList<ExpenseDto>> ListAllAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var expenses = await dbContext.Expenses
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Where(x => x.OccurredAt >= from && x.OccurredAt <= to)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return expenses.Select(Map).ToList();
    }

    public async Task<ServiceResult<ExpenseDto>> CreateAsync(
        ExpenseRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return ServiceResult<ExpenseDto>.Failure("validation", validation);
        }

        var now = timeProvider.GetUtcNow();
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Category = NormalizeCategory(request.Category),
            Description = request.Description.Trim(),
            Amount = request.Amount,
            OccurredAt = ResolveDate(request.OccurredAt),
            Note = NormalizeOptional(request.Note),
            CreatedBy = actorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            actorId,
            "expense.created",
            "expense",
            expense.Id.ToString(),
            new { expense.Category, expense.Amount, expense.OccurredAt },
            ipAddress,
            cancellationToken);

        var created = await dbContext.Expenses
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .SingleAsync(x => x.Id == expense.Id, cancellationToken);

        return ServiceResult<ExpenseDto>.Success(Map(created));
    }

    public async Task<ServiceResult<ExpenseDto>> UpdateAsync(
        Guid id,
        ExpenseRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var expense = await dbContext.Expenses
            .Include(x => x.CreatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (expense is null)
        {
            return ServiceResult<ExpenseDto>.Failure(
                "not_found", "Pengeluaran tidak ditemukan.");
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return ServiceResult<ExpenseDto>.Failure("validation", validation);
        }

        expense.Category = NormalizeCategory(request.Category);
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.OccurredAt = ResolveDate(request.OccurredAt);
        expense.Note = NormalizeOptional(request.Note);
        expense.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            actorId,
            "expense.updated",
            "expense",
            expense.Id.ToString(),
            new { expense.Category, expense.Amount, expense.OccurredAt },
            ipAddress,
            cancellationToken);

        return ServiceResult<ExpenseDto>.Success(Map(expense));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        Guid id,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var expense = await dbContext.Expenses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (expense is null)
        {
            return ServiceResult<bool>.Failure(
                "not_found", "Pengeluaran tidak ditemukan.");
        }

        dbContext.Expenses.Remove(expense);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            actorId,
            "expense.deleted",
            "expense",
            id.ToString(),
            new { expense.Category, expense.Amount, expense.OccurredAt },
            ipAddress,
            cancellationToken);

        return ServiceResult<bool>.Success(true);
    }

    private static string? Validate(ExpenseRequest request)
    {
        if (request is null)
        {
            return "Data pengeluaran wajib diisi.";
        }

        if (string.IsNullOrWhiteSpace(request.Category) ||
            !ExpenseCategories.All.Contains(
                request.Category.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            return "Kategori pengeluaran tidak valid.";
        }

        if (string.IsNullOrWhiteSpace(request.Description) ||
            request.Description.Trim().Length < 2)
        {
            return "Deskripsi minimal 2 karakter.";
        }

        if (request.Amount <= 0)
        {
            return "Nominal pengeluaran harus lebih dari 0.";
        }

        return null;
    }

    private static string NormalizeCategory(string category) =>
        ExpenseCategories.All.First(x =>
            string.Equals(x, category.Trim(), StringComparison.OrdinalIgnoreCase));

    private static DateOnly ResolveDate(DateOnly value) =>
        value == default ? ReportPeriod.Today() : value;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ExpenseDto Map(Expense expense) =>
        new(
            expense.Id,
            expense.Category,
            expense.Description,
            expense.Amount,
            expense.OccurredAt,
            expense.Note,
            expense.CreatedByUser?.Name,
            expense.CreatedAt,
            expense.UpdatedAt);
}
