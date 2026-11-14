using System.ComponentModel.DataAnnotations;

namespace Hanora.Application.Reporting;

public static class ExpenseCategories
{
    public static readonly string[] All =
    [
        "Bahan baku",
        "Operasional",
        "Perlengkapan",
        "SDM",
        "Lainnya"
    ];
}

public sealed record DashboardSummaryDto(
    DateOnly From,
    DateOnly To,
    decimal TotalRevenue,
    int TotalTransactions,
    int ActiveOrders,
    decimal TotalExpenses,
    decimal NetProfit,
    decimal AverageOrderValue,
    decimal QrisRevenue,
    decimal CashRevenue);

public sealed record SalesTrendPointDto(
    string Period,
    decimal Revenue,
    int Transactions);

public sealed record TopProductDto(
    Guid? ProductId,
    string ProductName,
    int QuantitySold,
    decimal Revenue);

public sealed record PaymentMethodBreakdownDto(
    string Method,
    decimal Revenue,
    int Transactions);

public sealed record ExpenseDto(
    Guid Id,
    string Category,
    string Description,
    decimal Amount,
    DateOnly OccurredAt,
    string? Note,
    string? CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class ExpenseRequest
{
    [Required(ErrorMessage = "Kategori pengeluaran wajib diisi.")]
    [MaxLength(40)]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Deskripsi wajib diisi.")]
    [MinLength(2, ErrorMessage = "Deskripsi minimal 2 karakter.")]
    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999_999_999, ErrorMessage = "Nominal pengeluaran harus lebih dari 0.")]
    public decimal Amount { get; set; }

    public DateOnly OccurredAt { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public sealed record IncomeDto(
    Guid Id,
    string Source,
    string Description,
    decimal Amount,
    DateOnly OccurredAt,
    string? OrderNumber,
    string? CreatedByName,
    DateTimeOffset CreatedAt);

public sealed class ManualIncomeRequest
{
    [Required(ErrorMessage = "Deskripsi wajib diisi.")]
    [MinLength(2, ErrorMessage = "Deskripsi minimal 2 karakter.")]
    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999_999_999, ErrorMessage = "Nominal pemasukan harus lebih dari 0.")]
    public decimal Amount { get; set; }

    public DateOnly OccurredAt { get; set; }
}

public sealed record ReportTransactionDto(
    string OrderNumber,
    string CustomerName,
    string TableName,
    decimal Total,
    string Method,
    string Status,
    DateTimeOffset? PaidAt);

public sealed record ReportDataDto(
    DashboardSummaryDto Summary,
    IReadOnlyList<ReportTransactionDto> Transactions,
    IReadOnlyList<ExpenseDto> Expenses);

public sealed record ReportFileResult(
    string ContentType,
    string FileName,
    byte[] Content);

public static class ReportPeriod
{
    private static readonly TimeZoneInfo JakartaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    public const int MaxDays = 366;

    public static DateOnly Today()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, JakartaTimeZone);
        return DateOnly.FromDateTime(now.DateTime);
    }

    public static bool TryResolve(
        DateOnly? from,
        DateOnly? to,
        out DateOnly resolvedFrom,
        out DateOnly resolvedTo,
        out string? error)
    {
        var today = Today();
        resolvedFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        resolvedTo = to ?? today;
        error = null;

        if (resolvedFrom > resolvedTo)
        {
            error = "Tanggal awal tidak boleh melebihi tanggal akhir.";
            return false;
        }

        if (resolvedTo.DayNumber - resolvedFrom.DayNumber > MaxDays)
        {
            error = "Rentang laporan maksimal 1 tahun per permintaan.";
            return false;
        }

        return true;
    }
}
