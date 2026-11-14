using Hanora.Application.Abstractions;
using Hanora.Application.Reporting;
using Hanora.Domain.Enums;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hanora.Infrastructure.Services;

public sealed class ReportingService(
    HanoraDbContext dbContext,
    IExpenseService expenseService) : IReportingService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var incomeQuery = dbContext.Incomes
            .AsNoTracking()
            .Where(x => x.OccurredAt >= from && x.OccurredAt <= to);

        var totalRevenue = await incomeQuery
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var totalTransactions = await incomeQuery.CountAsync(cancellationToken);

        var totalExpenses = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.OccurredAt >= from && x.OccurredAt <= to)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var activeOrders = await dbContext.Orders
            .AsNoTracking()
            .CountAsync(
                x => x.Status == OrderStatus.PendingPayment ||
                     x.Status == OrderStatus.PendingConfirmation ||
                     x.Status == OrderStatus.Processing ||
                     x.Status == OrderStatus.Ready,
                cancellationToken);

        var paidPayments = dbContext.Payments
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Paid &&
                        x.PaidAt != null &&
                        x.PaidAt >= StartOf(from) &&
                        x.PaidAt < StartOf(to.AddDays(1)));

        var qrisRevenue = await paidPayments
            .Where(x => x.Method == PaymentMethod.Qris)
            .SumAsync(x => (decimal?)x.GrossAmount, cancellationToken) ?? 0m;
        var cashRevenue = await paidPayments
            .Where(x => x.Method == PaymentMethod.Cash)
            .SumAsync(x => (decimal?)x.GrossAmount, cancellationToken) ?? 0m;

        return new DashboardSummaryDto(
            from,
            to,
            totalRevenue,
            totalTransactions,
            activeOrders,
            totalExpenses,
            totalRevenue - totalExpenses,
            totalTransactions == 0
                ? 0m
                : Math.Round(totalRevenue / totalTransactions, 2),
            qrisRevenue,
            cashRevenue);
    }

    public async Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken)
    {
        var monthly = string.Equals(
            groupBy?.Trim(),
            "month",
            StringComparison.OrdinalIgnoreCase);

        var rows = await dbContext.Incomes
            .AsNoTracking()
            .Where(x => x.OccurredAt >= from && x.OccurredAt <= to)
            .Select(x => new { x.OccurredAt, x.Amount })
            .ToListAsync(cancellationToken);

        var byDate = rows
            .GroupBy(x => monthly
                ? new DateOnly(x.OccurredAt.Year, x.OccurredAt.Month, 1)
                : x.OccurredAt)
            .ToDictionary(
                x => x.Key,
                x => (Revenue: x.Sum(y => y.Amount), Count: x.Count()));

        var points = new List<SalesTrendPointDto>();
        var cursor = monthly
            ? new DateOnly(from.Year, from.Month, 1)
            : from;

        while (cursor <= to)
        {
            byDate.TryGetValue(cursor, out var bucket);

            points.Add(new SalesTrendPointDto(
                monthly ? cursor.ToString("yyyy-MM") : cursor.ToString("yyyy-MM-dd"),
                bucket.Revenue,
                bucket.Count));

            cursor = monthly ? cursor.AddMonths(1) : cursor.AddDays(1);
        }

        return points;
    }

    public async Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 50);
        var start = from;
        var end = to;

        var rows = await (
            from item in dbContext.OrderItems.AsNoTracking()
            join order in dbContext.Orders.AsNoTracking()
                on item.OrderId equals order.Id
            join income in dbContext.Incomes.AsNoTracking()
                on order.Id equals income.OrderId
            where income.OccurredAt >= start && income.OccurredAt <= end
            group item by new { item.ProductId, item.ProductNameSnapshot }
            into grouped
            select new
            {
                grouped.Key.ProductId,
                grouped.Key.ProductNameSnapshot,
                Quantity = grouped.Sum(x => (int)x.Quantity),
                Revenue = grouped.Sum(x => x.Subtotal)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new TopProductDto(
                x.ProductId,
                x.ProductNameSnapshot,
                x.Quantity,
                x.Revenue))
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentMethodBreakdownDto>> GetPaymentMethodsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Payments
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Paid &&
                        x.PaidAt != null &&
                        x.PaidAt >= StartOf(from) &&
                        x.PaidAt < StartOf(to.AddDays(1)))
            .GroupBy(x => x.Method)
            .Select(x => new
            {
                Method = x.Key,
                Revenue = x.Sum(y => y.GrossAmount),
                Transactions = x.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new PaymentMethodBreakdownDto(
                MethodToString(x.Method),
                x.Revenue,
                x.Transactions))
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }

    public async Task<IReadOnlyList<ReportTransactionDto>> GetTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Order)
            .ThenInclude(x => x.Table)
            .Where(x => (x.Status == PaymentStatus.Paid ||
                         x.Status == PaymentStatus.Refunded) &&
                        x.PaidAt != null &&
                        x.PaidAt >= StartOf(from) &&
                        x.PaidAt < StartOf(to.AddDays(1)))
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

        return payments
            .Select(x => new ReportTransactionDto(
                x.Order.OrderNumber,
                x.Order.CustomerName,
                x.Order.Table.Name,
                x.Order.Total,
                MethodToString(x.Method),
                x.Status == PaymentStatus.Paid ? "paid" : "refunded",
                x.PaidAt))
            .ToList();
    }

    public async Task<ReportDataDto> GetReportDataAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var summary = await GetSummaryAsync(from, to, cancellationToken);
        var transactions = await GetTransactionsAsync(from, to, cancellationToken);
        var expenses = await expenseService.ListAllAsync(
            from, to, cancellationToken);

        return new ReportDataDto(summary, transactions, expenses);
    }

    private static DateTimeOffset StartOf(DateOnly date) =>
        new DateTimeOffset(
                date.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(7))
            .ToUniversalTime();

    private static string MethodToString(PaymentMethod method) =>
        method == PaymentMethod.Qris ? "qris" : "cash";
}
