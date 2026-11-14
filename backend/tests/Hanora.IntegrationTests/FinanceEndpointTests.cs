using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Hanora.IntegrationTests;

public sealed class FinanceEndpointTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    private static readonly DateOnly Today = TodayJakarta();

    [Fact]
    public async Task Dashboard_MenghitungPendapatanPengeluaranDanProdukTerlaris()
    {
        using var owner = await CreateOwnerClientAsync();

        var baseline = await owner.GetFromJsonAsync<DashboardSummaryResponse>(
            $"/api/v1/owner/dashboard/summary?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        var baselineMethods = await owner.GetFromJsonAsync<List<PaymentMethodResponse>>(
            $"/api/v1/owner/dashboard/payment-methods?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        var baselineTrend = await owner.GetFromJsonAsync<List<SalesTrendResponse>>(
            $"/api/v1/owner/dashboard/sales-trend?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&groupBy=day");

        var product = await CreateCatalogItemAsync(owner, "Kopi Uji", 50000m);
        var table = await CreateTableAsync(owner);
        var order = await CreateConfirmedCashOrderAsync(owner, product, table);
        var expense = await CreateExpenseAsync(owner, "Bahan baku", 20000m);

        Assert.NotEqual(Guid.Empty, expense.Id);

        var summary = await owner.GetFromJsonAsync<DashboardSummaryResponse>(
            $"/api/v1/owner/dashboard/summary?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");

        Assert.NotNull(summary);
        Assert.NotNull(baseline);
        Assert.Equal(baseline.TotalRevenue + 55000m, summary.TotalRevenue);
        Assert.Equal(baseline.TotalExpenses + 20000m, summary.TotalExpenses);
        Assert.Equal(baseline.NetProfit + 35000m, summary.NetProfit);
        Assert.Equal(baseline.TotalTransactions + 1, summary.TotalTransactions);
        Assert.Equal(baseline.CashRevenue + 55000m, summary.CashRevenue);

        var methods = await owner.GetFromJsonAsync<List<PaymentMethodResponse>>(
            $"/api/v1/owner/dashboard/payment-methods?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");

        Assert.NotNull(methods);
        Assert.NotNull(baselineMethods);
        var baselineCash = baselineMethods
            .FirstOrDefault(x => x.Method == "cash")?.Revenue ?? 0m;
        var cash = Assert.Single(methods, x => x.Method == "cash");
        Assert.Equal(baselineCash + 55000m, cash.Revenue);

        var topProducts = await owner.GetFromJsonAsync<List<TopProductResponse>>(
            $"/api/v1/owner/dashboard/top-products?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&limit=50");

        Assert.NotNull(topProducts);
        var top = Assert.Single(topProducts, x => x.ProductName == "Kopi Uji");
        Assert.Equal(1, top.QuantitySold);
        Assert.Equal(50000m, top.Revenue);

        var trend = await owner.GetFromJsonAsync<List<SalesTrendResponse>>(
            $"/api/v1/owner/dashboard/sales-trend?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&groupBy=day");

        Assert.NotNull(trend);
        Assert.NotNull(baselineTrend);
        var baselinePoint = baselineTrend.FirstOrDefault(
            x => x.Period == Today.ToString("yyyy-MM-dd"))?.Revenue ?? 0m;
        var point = Assert.Single(trend);
        Assert.Equal(Today.ToString("yyyy-MM-dd"), point.Period);
        Assert.Equal(baselinePoint + 55000m, point.Revenue);

        var report = await owner.GetFromJsonAsync<ReportDataResponse>(
            $"/api/v1/owner/reports/data?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");

        Assert.NotNull(report);
        Assert.Equal(summary.TotalRevenue, report.Summary.TotalRevenue);
        Assert.Equal(summary.TotalExpenses, report.Summary.TotalExpenses);
        Assert.Contains(report.Transactions, x => x.OrderNumber == order.OrderNumber);
        Assert.Contains(report.Expenses, x => x.Id == expense.Id);
    }

    [Fact]
    public async Task Pengeluaran_DapatDibuatDiubahDanDihapus()
    {
        using var owner = await CreateOwnerClientAsync();
        var expense = await CreateExpenseAsync(owner, "Operasional", 15000m, "Listrik");

        var update = await owner.PutAsJsonAsync(
            $"/api/v1/owner/expenses/{expense.Id}",
            new
            {
                category = "Operasional",
                description = "Listrik dan air",
                amount = 17500m,
                occurredAt = Today.ToString("yyyy-MM-dd"),
                note = "Revisi"
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(updated);
        Assert.Equal(17500m, updated.Amount);
        Assert.Equal("Listrik dan air", updated.Description);

        var invalid = await owner.PostAsJsonAsync("/api/v1/owner/expenses", new
        {
            category = "Kategori Palsu",
            description = "Tidak valid",
            amount = 10000m,
            occurredAt = Today.ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var delete = await owner.DeleteAsync($"/api/v1/owner/expenses/{expense.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await owner.GetFromJsonAsync<PagedResponse<ExpenseResponse>>(
            $"/api/v1/owner/expenses?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");

        Assert.NotNull(list);
        Assert.DoesNotContain(list.Items, x => x.Id == expense.Id);
    }

    [Fact]
    public async Task PemasukanManual_DapatDihapus_TetapiPemasukanPesananTidak()
    {
        using var owner = await CreateOwnerClientAsync();

        var manualResponse = await owner.PostAsJsonAsync("/api/v1/owner/incomes", new
        {
            description = "Penjualan merchandise",
            amount = 25000m,
            occurredAt = Today.ToString("yyyy-MM-dd")
        });

        Assert.Equal(HttpStatusCode.Created, manualResponse.StatusCode);
        var manual = await manualResponse.Content.ReadFromJsonAsync<IncomeResponse>();
        Assert.NotNull(manual);
        Assert.Equal("manual", manual.Source);

        var deleteManual = await owner.DeleteAsync(
            $"/api/v1/owner/incomes/{manual.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteManual.StatusCode);

        var product = await CreateCatalogItemAsync(owner, "Teh Uji", 20000m);
        var table = await CreateTableAsync(owner);
        var order = await CreateConfirmedCashOrderAsync(owner, product, table);

        var incomes = await owner.GetFromJsonAsync<PagedResponse<IncomeResponse>>(
            $"/api/v1/owner/incomes?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&pageSize=100");

        Assert.NotNull(incomes);
        var orderIncome = Assert.Single(
            incomes.Items,
            x => x.Source == "order" &&
                 x.Description == $"Pembayaran {order.OrderNumber}");

        var deleteOrderIncome = await owner.DeleteAsync(
            $"/api/v1/owner/incomes/{orderIncome.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteOrderIncome.StatusCode);
    }

    [Fact]
    public async Task ExportLaporan_MenghasilkanPdfDanXlsx()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, "Matcha Uji", 40000m);
        var table = await CreateTableAsync(owner);
        await CreateConfirmedCashOrderAsync(owner, product, table);
        await CreateExpenseAsync(owner, "Perlengkapan", 12000m, "Cup");

        var pdf = await owner.GetAsync(
            $"/api/v1/owner/reports/export?format=pdf&type=full&from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 4));

        var xlsx = await owner.GetAsync(
            $"/api/v1/owner/reports/export?format=xlsx&type=full&from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Contains(
            "spreadsheetml",
            xlsx.Content.Headers.ContentType?.MediaType ?? string.Empty);
        var xlsxBytes = await xlsx.Content.ReadAsByteArrayAsync();
        Assert.True(xlsxBytes.Length > 1000);
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(xlsxBytes, 0, 2));

        var invalid = await owner.GetAsync(
            $"/api/v1/owner/reports/export?format=docx&type=full&from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task RentangTanggalTidakValid_Ditolak()
    {
        using var owner = await CreateOwnerClientAsync();
        var from = Today.AddDays(2);
        var to = Today;

        var response = await owner.GetAsync(
            $"/api/v1/owner/dashboard/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = AuthWebApplicationFactory.OwnerEmail,
            password = AuthWebApplicationFactory.OwnerPassword
        });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<ExpenseResponse> CreateExpenseAsync(
        HttpClient owner,
        string category,
        decimal amount,
        string description = "Pembelian bahan")
    {
        var response = await owner.PostAsJsonAsync("/api/v1/owner/expenses", new
        {
            category,
            description,
            amount,
            occurredAt = Today.ToString("yyyy-MM-dd"),
            note = "Uji integrasi"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var expense = await response.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(expense);
        return expense;
    }

    private static async Task<ProductResponse> CreateCatalogItemAsync(
        HttpClient owner,
        string name,
        decimal price)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryResponse = await owner.PostAsJsonAsync(
            "/api/v1/owner/categories",
            new
            {
                name = $"Test {suffix}",
                sortOrder = 1,
                isActive = true
            });
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();

        var productResponse = await owner.PostAsJsonAsync(
            "/api/v1/owner/products",
            new
            {
                name,
                categoryId = category!.Id,
                description = "Produk uji laporan.",
                price,
                isAvailable = true
            });
        productResponse.EnsureSuccessStatusCode();
        return (await productResponse.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static async Task<TableResponse> CreateTableAsync(HttpClient owner)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var response = await owner.PostAsJsonAsync("/api/v1/owner/tables", new
        {
            number = $"F{suffix}",
            name = $"Meja Laporan {suffix}",
            capacity = 2,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TableResponse>())!;
    }

    private async Task<OrderResponse> CreateConfirmedCashOrderAsync(
        HttpClient owner,
        ProductResponse product,
        TableResponse table)
    {
        var token = table.QrUrl!.Split("/t/", StringSplitOptions.RemoveEmptyEntries)[^1];

        using var customer = factory.CreateClient();

        var session = await customer.GetFromJsonAsync<TableSessionResponse>(
            $"/api/v1/public/tables/{token}");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", session!.TableSessionToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            customerName = "Customer Laporan",
            paymentMethod = "cash",
            items = new[] { new { productId = product.Id, quantity = 1 } }
        });

        var orderResponse = await customer.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var queue = await owner.GetFromJsonAsync<PagedCashierOrders>(
            "/api/v1/cashier/orders?pageSize=100");
        var cashierOrder = Assert.Single(
            queue!.Items,
            x => x.OrderNumber == order.OrderNumber);

        var confirm = await owner.PostAsJsonAsync(
            $"/api/v1/cashier/orders/{cashierOrder.Id}/confirm-cash",
            new { note = "Tunai diterima" });
        confirm.EnsureSuccessStatusCode();

        return order;
    }

    private static DateOnly TodayJakarta()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(now.DateTime);
    }

    private sealed record LoginResponse(string AccessToken);

    private sealed record CategoryResponse(Guid Id);

    private sealed record ProductResponse(Guid Id, string Name, decimal Price);

    private sealed record TableResponse(Guid Id, string Number, string Name, string? QrUrl);

    private sealed record TableSessionResponse(string TableSessionToken);

    private sealed record OrderResponse(string OrderNumber);

    private sealed record CashierOrderSummary(
        Guid Id,
        string OrderNumber);

    private sealed record PagedCashierOrders(
        IReadOnlyList<CashierOrderSummary> Items);

    private sealed record DashboardSummaryResponse(
        decimal TotalRevenue,
        int TotalTransactions,
        int ActiveOrders,
        decimal TotalExpenses,
        decimal NetProfit,
        decimal AverageOrderValue,
        decimal QrisRevenue,
        decimal CashRevenue);

    private sealed record SalesTrendResponse(
        string Period,
        decimal Revenue,
        int Transactions);

    private sealed record TopProductResponse(
        Guid? ProductId,
        string ProductName,
        int QuantitySold,
        decimal Revenue);

    private sealed record PaymentMethodResponse(
        string Method,
        decimal Revenue,
        int Transactions);

    private sealed record ExpenseResponse(
        Guid Id,
        string Category,
        string Description,
        decimal Amount);

    private sealed record IncomeResponse(
        Guid Id,
        string Source,
        string Description,
        decimal Amount);

    private sealed record ReportTransactionResponse(
        string OrderNumber,
        decimal Total,
        string Method);

    private sealed record ReportDataResponse(
        DashboardSummaryResponse Summary,
        IReadOnlyList<ReportTransactionResponse> Transactions,
        IReadOnlyList<ExpenseResponse> Expenses);

    private sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalItems);
}
