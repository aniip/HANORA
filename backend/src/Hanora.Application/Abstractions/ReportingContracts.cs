using Hanora.Application.Common;
using Hanora.Application.Reporting;

namespace Hanora.Application.Abstractions;

public interface IReportingService
{
    Task<DashboardSummaryDto> GetSummaryAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TopProductDto>> GetTopProductsAsync(
        DateOnly from,
        DateOnly to,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentMethodBreakdownDto>> GetPaymentMethodsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportTransactionDto>> GetTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<ReportDataDto> GetReportDataAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

public interface IExpenseService
{
    Task<PagedResult<ExpenseDto>> ListAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseDto>> ListAllAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExpenseDto>> CreateAsync(
        ExpenseRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<ExpenseDto>> UpdateAsync(
        Guid id,
        ExpenseRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteAsync(
        Guid id,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}

public interface IIncomeService
{
    Task<PagedResult<IncomeDto>> ListAsync(
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomeDto>> ListAllAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<ServiceResult<IncomeDto>> CreateManualAsync(
        ManualIncomeRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteAsync(
        Guid id,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}

public interface IReportExportService
{
    Task<ServiceResult<ReportFileResult>> ExportAsync(
        string format,
        string type,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
