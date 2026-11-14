using System.Globalization;
using ClosedXML.Excel;
using Hanora.Application.Abstractions;
using Hanora.Application.Common;
using Hanora.Application.Reporting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Hanora.Infrastructure.Services;

public sealed class ReportExportService(
    IReportingService reportingService,
    IExpenseService expenseService) : IReportExportService
{
    private static readonly string[] SupportedTypes =
    [
        "summary",
        "transactions",
        "expenses",
        "full"
    ];

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<ServiceResult<ReportFileResult>> ExportAsync(
        string format,
        string type,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var normalizedFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedType = (type ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedFormat is not ("pdf" or "xlsx"))
        {
            return ServiceResult<ReportFileResult>.Failure(
                "validation", "Format export harus pdf atau xlsx.");
        }

        if (!SupportedTypes.Contains(normalizedType))
        {
            return ServiceResult<ReportFileResult>.Failure(
                "validation",
                "Tipe laporan harus summary, transactions, expenses, atau full.");
        }

        var summary = await reportingService.GetSummaryAsync(
            from, to, cancellationToken);

        IReadOnlyList<ReportTransactionDto> transactions =
            normalizedType is "transactions" or "full"
                ? await reportingService.GetTransactionsAsync(
                    from, to, cancellationToken)
                : [];

        IReadOnlyList<ExpenseDto> expenses =
            normalizedType is "expenses" or "full"
                ? await expenseService.ListAllAsync(from, to, cancellationToken)
                : [];

        var fileName =
            $"Laporan-Hanora-{from:yyyy-MM-dd}_{to:yyyy-MM-dd}." +
            (normalizedFormat == "pdf" ? "pdf" : "xlsx");

        var content = normalizedFormat == "pdf"
            ? BuildPdf(summary, normalizedType, transactions, expenses)
            : BuildExcel(summary, normalizedType, transactions, expenses);

        return ServiceResult<ReportFileResult>.Success(
            new ReportFileResult(
                normalizedFormat == "pdf"
                    ? "application/pdf"
                    : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName,
                content));
    }

    private static byte[] BuildPdf(
        DashboardSummaryDto summary,
        string type,
        IReadOnlyList<ReportTransactionDto> transactions,
        IReadOnlyList<ExpenseDto> expenses) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item()
                        .Text("HANORA CAFE")
                        .FontSize(18)
                        .SemiBold()
                        .FontColor(Colors.Brown.Darken3);
                    header.Item()
                        .Text("Laporan Keuangan")
                        .FontSize(12)
                        .SemiBold();
                    header.Item()
                        .PaddingTop(2)
                        .Text($"Periode {FormatDate(summary.From)} — {FormatDate(summary.To)}")
                        .FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(12).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Text("Ringkasan").SemiBold().FontSize(12);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        AddSummaryCells(
                            table,
                            "Pendapatan", Money(summary.TotalRevenue),
                            "Pengeluaran", Money(summary.TotalExpenses));
                        AddSummaryCells(
                            table,
                            "Laba bersih", Money(summary.NetProfit),
                            "Transaksi", summary.TotalTransactions.ToString("N0", CultureInfo.InvariantCulture));
                        AddSummaryCells(
                            table,
                            "Pesanan aktif", summary.ActiveOrders.ToString("N0", CultureInfo.InvariantCulture),
                            "Rata-rata/transaksi", Money(summary.AverageOrderValue));
                        AddSummaryCells(
                            table,
                            "Pendapatan QRIS", Money(summary.QrisRevenue),
                            "Pendapatan tunai", Money(summary.CashRevenue));
                    });

                    if (type is "transactions" or "full")
                    {
                        column.Item().Text("Transaksi").SemiBold().FontSize(12);
                        AddTransactionTable(column, transactions);
                    }

                    if (type is "expenses" or "full")
                    {
                        column.Item().Text("Pengeluaran").SemiBold().FontSize(12);
                        AddExpenseTable(column, expenses);
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Halaman ");
                        text.CurrentPageNumber();
                        text.Span(" dari ");
                        text.TotalPages();
                    });
            });
        }).GeneratePdf();

    private static void AddSummaryCells(
        TableDescriptor table,
        string label1,
        string value1,
        string label2,
        string value2)
    {
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(label1).SemiBold();
        table.Cell().Padding(4).AlignRight().Text(value1);
        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(label2).SemiBold();
        table.Cell().Padding(4).AlignRight().Text(value2);
    }

    private static void AddTransactionTable(
        ColumnDescriptor column,
        IReadOnlyList<ReportTransactionDto> transactions)
    {
        if (transactions.Count == 0)
        {
            column.Item().Text("Tidak ada transaksi pada periode ini.")
                .FontColor(Colors.Grey.Darken1);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.6f);
            });

            table.Header(header =>
            {
                AddHeaderCell(header, "No");
                AddHeaderCell(header, "No. Pesanan");
                AddHeaderCell(header, "Customer");
                AddHeaderCell(header, "Meja");
                AddHeaderCell(header, "Metode");
                AddHeaderCell(header, "Total", true);
            });

            var index = 0;

            foreach (var transaction in transactions)
            {
                index++;

                table.Cell().Padding(3).Text(index.ToString(CultureInfo.InvariantCulture));
                table.Cell().Padding(3).Text(transaction.OrderNumber);
                table.Cell().Padding(3).Text(transaction.CustomerName);
                table.Cell().Padding(3).Text(transaction.TableName);
                table.Cell().Padding(3).Text(MethodLabel(transaction.Method));
                table.Cell().Padding(3).AlignRight().Text(Money(transaction.Total));
            }
        });
    }

    private static void AddExpenseTable(
        ColumnDescriptor column,
        IReadOnlyList<ExpenseDto> expenses)
    {
        if (expenses.Count == 0)
        {
            column.Item().Text("Tidak ada pengeluaran pada periode ini.")
                .FontColor(Colors.Grey.Darken1);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(22);
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(3);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.6f);
            });

            table.Header(header =>
            {
                AddHeaderCell(header, "No");
                AddHeaderCell(header, "Kategori");
                AddHeaderCell(header, "Deskripsi");
                AddHeaderCell(header, "Tanggal");
                AddHeaderCell(header, "Nominal", true);
            });

            var index = 0;

            foreach (var expense in expenses)
            {
                index++;

                table.Cell().Padding(3).Text(index.ToString(CultureInfo.InvariantCulture));
                table.Cell().Padding(3).Text(expense.Category);
                table.Cell().Padding(3).Text(expense.Description);
                table.Cell().Padding(3).Text(FormatDate(expense.OccurredAt));
                table.Cell().Padding(3).AlignRight().Text(Money(expense.Amount));
            }
        });
    }

    private static void AddHeaderCell(
        TableCellDescriptor header,
        string text,
        bool alignRight = false)
    {
        var cell = header.Cell()
            .Background(Colors.Brown.Lighten4)
            .Padding(4);

        if (alignRight)
        {
            cell.AlignRight().Text(text).SemiBold();
            return;
        }

        cell.Text(text).SemiBold();
    }

    private static byte[] BuildExcel(
        DashboardSummaryDto summary,
        string type,
        IReadOnlyList<ReportTransactionDto> transactions,
        IReadOnlyList<ExpenseDto> expenses)
    {
        using var workbook = new XLWorkbook();

        var summarySheet = workbook.Worksheets.Add("Ringkasan");
        summarySheet.Cell(1, 1).Value = "HANORA CAFE";
        summarySheet.Cell(1, 1).Style.Font.Bold = true;
        summarySheet.Cell(1, 1).Style.Font.FontSize = 16;
        summarySheet.Cell(2, 1).Value = "Laporan Keuangan";
        summarySheet.Cell(2, 1).Style.Font.Bold = true;
        summarySheet.Cell(3, 1).Value =
            $"Periode {FormatDate(summary.From)} - {FormatDate(summary.To)}";
        summarySheet.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;

        var summaryRows = new (string Label, object Value, bool Money)[]
        {
            ("Pendapatan", summary.TotalRevenue, true),
            ("Pengeluaran", summary.TotalExpenses, true),
            ("Laba bersih", summary.NetProfit, true),
            ("Jumlah transaksi", summary.TotalTransactions, false),
            ("Pesanan aktif", summary.ActiveOrders, false),
            ("Rata-rata per transaksi", summary.AverageOrderValue, true),
            ("Pendapatan QRIS", summary.QrisRevenue, true),
            ("Pendapatan tunai", summary.CashRevenue, true)
        };

        var row = 5;

        foreach (var item in summaryRows)
        {
            summarySheet.Cell(row, 1).Value = item.Label;
            summarySheet.Cell(row, 2).Value = XLCellValue.FromObject(item.Value);
            summarySheet.Cell(row, 1).Style.Font.Bold = true;

            if (item.Money)
            {
                summarySheet.Cell(row, 2).Style.NumberFormat.Format = "\"Rp\" #,##0";
            }

            row++;
        }

        summarySheet.Columns().AdjustToContents();

        if (type is "transactions" or "full")
        {
            var sheet = workbook.Worksheets.Add("Transaksi");
            var headers = new[]
            {
                "No", "No. Pesanan", "Customer", "Meja", "Metode",
                "Status", "Total", "Dibayar"
            };

            for (var column = 0; column < headers.Length; column++)
            {
                sheet.Cell(1, column + 1).Value = headers[column];
            }

            StyleHeader(sheet, headers.Length);

            row = 2;

            foreach (var transaction in transactions)
            {
                sheet.Cell(row, 1).Value = row - 1;
                sheet.Cell(row, 2).Value = transaction.OrderNumber;
                sheet.Cell(row, 3).Value = transaction.CustomerName;
                sheet.Cell(row, 4).Value = transaction.TableName;
                sheet.Cell(row, 5).Value = MethodLabel(transaction.Method);
                sheet.Cell(row, 6).Value = transaction.Status == "paid"
                    ? "Lunas"
                    : "Dikembalikan";
                sheet.Cell(row, 7).Value = transaction.Total;
                sheet.Cell(row, 7).Style.NumberFormat.Format = "\"Rp\" #,##0";

                if (transaction.PaidAt is not null)
                {
                    sheet.Cell(row, 8).Value = transaction.PaidAt.Value
                        .ToOffset(TimeSpan.FromHours(7))
                        .DateTime;
                    sheet.Cell(row, 8).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                }

                row++;
            }

            sheet.Columns().AdjustToContents();
        }

        if (type is "expenses" or "full")
        {
            var sheet = workbook.Worksheets.Add("Pengeluaran");
            var headers = new[]
            {
                "No", "Kategori", "Deskripsi", "Nominal", "Tanggal", "Catatan"
            };

            for (var column = 0; column < headers.Length; column++)
            {
                sheet.Cell(1, column + 1).Value = headers[column];
            }

            StyleHeader(sheet, headers.Length);

            row = 2;

            foreach (var expense in expenses)
            {
                sheet.Cell(row, 1).Value = row - 1;
                sheet.Cell(row, 2).Value = expense.Category;
                sheet.Cell(row, 3).Value = expense.Description;
                sheet.Cell(row, 4).Value = expense.Amount;
                sheet.Cell(row, 4).Style.NumberFormat.Format = "\"Rp\" #,##0";
                sheet.Cell(row, 5).Value = expense.OccurredAt.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 5).Style.DateFormat.Format = "yyyy-mm-dd";
                sheet.Cell(row, 6).Value = expense.Note ?? string.Empty;
                row++;
            }

            sheet.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void StyleHeader(IXLWorksheet sheet, int columnCount)
    {
        var range = sheet.Range(1, 1, 1, columnCount);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFE5D8");
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("id-ID"));

    private static string Money(decimal value) =>
        "Rp " + value.ToString("N0", CultureInfo.GetCultureInfo("id-ID"));

    private static string MethodLabel(string method) =>
        string.Equals(method, "qris", StringComparison.OrdinalIgnoreCase)
            ? "QRIS"
            : "Tunai";
}
