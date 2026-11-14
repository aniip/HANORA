using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hanora.IntegrationTests;

public sealed class OrderEndpointTests(AuthWebApplicationFactory factory)
    : IClassFixture<AuthWebApplicationFactory>
{
    [Fact]
    public async Task AlurQrMeja_HinggaPesananDanTracking()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 32000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(
            customer, tableToken, product.Id, quantity: 2, paymentMethod: "cash");

        Assert.Equal("pending_confirmation", order.Status);
        Assert.Equal(64000m, order.Subtotal);
        Assert.Equal(10m, order.TaxRate);
        Assert.Equal(6400m, order.TaxAmount);
        Assert.Equal(70400m, order.Total);
        Assert.Equal("cash", order.Payment.Method);
        Assert.Equal("pending", order.Payment.Status);
        Assert.False(string.IsNullOrWhiteSpace(order.TrackingToken));

        var tracking = await customer.GetFromJsonAsync<OrderTrackingResponse>(
            $"/api/v1/orders/track/{order.TrackingToken}");
        Assert.NotNull(tracking);
        Assert.Equal(order.OrderNumber, tracking.OrderNumber);
        Assert.Equal("pending_confirmation", tracking.Status);
        Assert.Contains(tracking.Timeline, x => x.Status == "pending_confirmation");

        var cashier = await owner.GetFromJsonAsync<PagedCashierOrders>(
            "/api/v1/cashier/orders?pageSize=100");
        Assert.Contains(cashier!.Items, x => x.OrderNumber == order.OrderNumber);
    }

    [Fact]
    public async Task HargaDariClient_Diabaikan_DanDihitungServer()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 25000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(
            customer,
            tableToken,
            product.Id,
            quantity: 3,
            paymentMethod: "cash",
            extraFields: new
            {
                price = 1,
                subtotal = 1,
                total = 1
            });

        Assert.Equal(75000m, order.Subtotal);
        Assert.Equal(7500m, order.TaxAmount);
        Assert.Equal(82500m, order.Total);
    }

    [Fact]
    public async Task IdempotencyKey_MencegahPesananGanda()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 20000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);
        var idempotencyKey = Guid.NewGuid().ToString();

        using var customer = factory.CreateClient();
        var first = await CreateOrderAsync(
            customer, tableToken, product.Id, 1, "cash", idempotencyKey: idempotencyKey);
        var second = await CreateOrderAsync(
            customer, tableToken, product.Id, 1, "cash", idempotencyKey: idempotencyKey);

        Assert.Equal(first.OrderNumber, second.OrderNumber);

        var cashier = await owner.GetFromJsonAsync<PagedCashierOrders>(
            $"/api/v1/cashier/orders?search={first.OrderNumber}");
        Assert.NotNull(cashier);
        Assert.Equal(1, cashier.Items.Count(x => x.OrderNumber == first.OrderNumber));
    }

    [Fact]
    public async Task ProdukTidakTersedia_Ditolak()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 18000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/owner/products/{product.Id}/availability",
            new { isAvailable = false });
        patch.EnsureSuccessStatusCode();

        using var customer = factory.CreateClient();
        var response = await PostOrderAsync(
            customer, tableToken, product.Id, 1, "cash", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegenerateQr_MencabutTokenLama()
    {
        using var owner = await CreateOwnerClientAsync();
        var table = await CreateTableAsync(owner);
        var oldToken = ExtractQrToken(table.QrUrl!);
        var oldResolve = await factory.CreateClient().GetAsync(
            $"/api/v1/public/tables/{oldToken}");
        Assert.Equal(HttpStatusCode.OK, oldResolve.StatusCode);

        var regenerate = await owner.PostAsync(
            $"/api/v1/owner/tables/{table.Id}/qr/regenerate", null);
        regenerate.EnsureSuccessStatusCode();
        var updated = await regenerate.Content.ReadFromJsonAsync<TableResponse>();
        Assert.NotNull(updated);
        var newToken = ExtractQrToken(updated.QrUrl!);

        var oldAfter = await factory.CreateClient().GetAsync(
            $"/api/v1/public/tables/{oldToken}");
        Assert.Equal(HttpStatusCode.NotFound, oldAfter.StatusCode);

        var newAfter = await factory.CreateClient().GetAsync(
            $"/api/v1/public/tables/{newToken}");
        Assert.Equal(HttpStatusCode.OK, newAfter.StatusCode);
    }

    [Fact]
    public async Task QrImage_DapatDiunduh()
    {
        using var owner = await CreateOwnerClientAsync();
        var table = await CreateTableAsync(owner);

        var response = await owner.GetAsync(
            $"/api/v1/owner/tables/{table.Id}/qr?format=png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 100);
    }

    [Fact]
    public async Task TokenMeja_TidakBisaAksesEndpointOwner()
    {
        using var owner = await CreateOwnerClientAsync();
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        customer.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tableToken);

        var response = await customer.GetAsync("/api/v1/owner/tables");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffToken_TidakBisaMembuatPesananCustomer()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 20000m);
        var table = await CreateTableAsync(owner);

        var response = await owner.PostAsJsonAsync("/api/v1/orders", new
        {
            customerName = "Bukan Customer",
            paymentMethod = "cash",
            items = new[] { new { productId = product.Id, quantity = 1 } }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_DapatMembatalkanPesanan()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 22000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(
            customer, tableToken, product.Id, 1, "cash");

        var cancel = await customer.PostAsJsonAsync(
            $"/api/v1/orders/track/{order.TrackingToken}/cancel",
            new { reason = "Salah pilih menu" });

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<OrderTrackingResponse>();
        Assert.NotNull(cancelled);
        Assert.Equal("cancelled", cancelled.Status);

        var secondCancel = await customer.PostAsJsonAsync(
            $"/api/v1/orders/track/{order.TrackingToken}/cancel",
            new { reason = "Lagi" });
        Assert.Equal(HttpStatusCode.Conflict, secondCancel.StatusCode);
    }

    [Fact]
    public async Task Kasir_KonfirmasiTunai_MengubahStatusDanMembuatTransaksi()
    {
        using var owner = await CreateOwnerClientAsync();
        var (order, cashierOrderId) = await CreateConfirmedOrderAsync(owner);

        var tracking = await factory.CreateClient().GetFromJsonAsync<OrderTrackingResponse>(
            $"/api/v1/orders/track/{order.TrackingToken}");
        Assert.NotNull(tracking);
        Assert.Equal("processing", tracking.Status);
        Assert.Equal("paid", tracking.Payment.Status);

        var transactions = await owner.GetFromJsonAsync<PagedTransactionsResponse>(
            "/api/v1/cashier/transactions?pageSize=100");
        Assert.NotNull(transactions);
        Assert.Contains(
            transactions.Items,
            x => x.OrderNumber == order.OrderNumber &&
                 x.Method == "cash" &&
                 x.Status == "paid");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HanoraDbContext>();
        Assert.True(await dbContext.Incomes.AnyAsync(x => x.OrderId == cashierOrderId));
    }

    [Fact]
    public async Task Kasir_UpdateStatus_ReadyLaluCompleted()
    {
        using var owner = await CreateOwnerClientAsync();
        var (order, cashierOrderId) = await CreateConfirmedOrderAsync(owner);

        var ready = await owner.PatchAsJsonAsync(
            $"/api/v1/cashier/orders/{cashierOrderId}/status",
            new { toStatus = "ready", note = "Siap diantar" });
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var readyDetail = await ready.Content.ReadFromJsonAsync<CashierOrderDetailResponse>();
        Assert.NotNull(readyDetail);
        Assert.Equal("ready", readyDetail.Status);

        var completed = await owner.PatchAsJsonAsync(
            $"/api/v1/cashier/orders/{cashierOrderId}/status",
            new { toStatus = "completed", note = "Selesai" });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var completedDetail = await completed.Content.ReadFromJsonAsync<CashierOrderDetailResponse>();
        Assert.NotNull(completedDetail);
        Assert.Equal("completed", completedDetail.Status);

        var invalid = await owner.PatchAsJsonAsync(
            $"/api/v1/cashier/orders/{cashierOrderId}/status",
            new { toStatus = "ready" });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
    }

    [Fact]
    public async Task Kasir_MembatalkanPesananPending()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 27000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(customer, tableToken, product.Id, 1, "cash");
        var cashierOrderId = await FindCashierOrderIdAsync(owner, order.OrderNumber);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/cashier/orders/{cashierOrderId}/cancel",
            new { reason = "Customer tidak melanjutkan pembayaran" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<CashierOrderDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("cancelled", detail.Status);
        Assert.Equal("cancelled", detail.Payment.Status);

        var tracking = await customer.GetFromJsonAsync<OrderTrackingResponse>(
            $"/api/v1/orders/track/{order.TrackingToken}");
        Assert.NotNull(tracking);
        Assert.Equal("cancelled", tracking.Status);
    }

    [Fact]
    public async Task Kasir_TidakBisaKonfirmasiTunaiUntukPesananQris()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 29000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(customer, tableToken, product.Id, 1, "qris");
        var cashierOrderId = await FindCashierOrderIdAsync(owner, order.OrderNumber);

        var response = await owner.PostAsJsonAsync(
            $"/api/v1/cashier/orders/{cashierOrderId}/confirm-cash",
            new { note = "Tunai" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Qris_Simulasi_MenandaiLunasDanMemprosesPesanan()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 31000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(customer, tableToken, product.Id, 1, "qris");
        Assert.Equal("pending_payment", order.Status);
        Assert.Equal("qris", order.Payment.Method);
        Assert.Equal("pending", order.Payment.Status);

        var initiate = await customer.PostAsync(
            $"/api/v1/orders/track/{order.TrackingToken}/payments/qris",
            null);
        Assert.Equal(HttpStatusCode.OK, initiate.StatusCode);
        var payment = await initiate.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(payment);
        Assert.True(payment.Simulation);
        Assert.False(string.IsNullOrWhiteSpace(payment.RedirectUrl));

        var simulate = await customer.PostAsync(
            $"/api/v1/orders/track/{order.TrackingToken}/simulate",
            null);
        Assert.Equal(HttpStatusCode.OK, simulate.StatusCode);
        var tracking = await simulate.Content.ReadFromJsonAsync<OrderTrackingResponse>();
        Assert.NotNull(tracking);
        Assert.Equal("processing", tracking.Status);
        Assert.Equal("paid", tracking.Payment.Status);
        Assert.NotNull(tracking.Payment.PaidAt);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HanoraDbContext>();
        var stored = await dbContext.Orders
            .SingleAsync(x => x.OrderNumber == order.OrderNumber);
        Assert.True(await dbContext.Incomes.AnyAsync(x => x.OrderId == stored.Id));
    }

    [Fact]
    public async Task Webhook_SignatureSalah_Ditolak()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 33000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(customer, tableToken, product.Id, 1, "qris");
        var providerOrderId = $"{order.OrderNumber}-P1";
        var payload = BuildMidtransPayload(providerOrderId, "settlement", order.Total);
        payload["signature_key"] = "signature-palsu";

        var response = await customer.PostAsJsonAsync(
            "/api/v1/webhooks/midtrans",
            payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var tracking = await customer.GetFromJsonAsync<OrderTrackingResponse>(
            $"/api/v1/orders/track/{order.TrackingToken}");
        Assert.NotNull(tracking);
        Assert.Equal("pending_payment", tracking.Status);
        Assert.Equal("pending", tracking.Payment.Status);
    }

    [Fact]
    public async Task Webhook_Valid_Idempoten_DanIncomeSekali()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 35000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(customer, tableToken, product.Id, 1, "qris");
        var providerOrderId = $"{order.OrderNumber}-P1";
        var payload = BuildMidtransPayload(providerOrderId, "settlement", order.Total);

        var first = await customer.PostAsJsonAsync(
            "/api/v1/webhooks/midtrans",
            payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<WebhookMessageResponse>();
        Assert.NotNull(firstBody);
        Assert.Equal("ok", firstBody.Message);

        var second = await customer.PostAsJsonAsync(
            "/api/v1/webhooks/midtrans",
            payload);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<WebhookMessageResponse>();
        Assert.NotNull(secondBody);
        Assert.Equal("duplicate_notification", secondBody.Message);

        var tracking = await customer.GetFromJsonAsync<OrderTrackingResponse>(
            $"/api/v1/orders/track/{order.TrackingToken}");
        Assert.NotNull(tracking);
        Assert.Equal("processing", tracking.Status);
        Assert.Equal("paid", tracking.Payment.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HanoraDbContext>();
        var stored = await dbContext.Orders
            .SingleAsync(x => x.OrderNumber == order.OrderNumber);
        Assert.Equal(
            1,
            await dbContext.Incomes.CountAsync(x => x.OrderId == stored.Id));
    }

    [Fact]
    public async Task Webhook_NominalTidakCocok_MenandaiReview()
    {
        using var owner = await CreateOwnerClientAsync();
        var product = await CreateCatalogItemAsync(owner, 37000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);

        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(customer, tableToken, product.Id, 1, "qris");
        var providerOrderId = $"{order.OrderNumber}-P1";
        var payload = BuildMidtransPayload(
            providerOrderId,
            "settlement",
            order.Total + 500m);

        var response = await customer.PostAsJsonAsync(
            "/api/v1/webhooks/midtrans",
            payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WebhookMessageResponse>();
        Assert.NotNull(body);
        Assert.Equal("amount_mismatch", body.Message);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HanoraDbContext>();
        var payment = await dbContext.Payments
            .SingleAsync(x => x.ProviderOrderId == providerOrderId);
        Assert.Equal("pending", payment.Status.ToString().ToLowerInvariant());
        Assert.True(payment.NeedsReview);
    }

    private static Dictionary<string, object?> BuildMidtransPayload(
        string providerOrderId,
        string transactionStatus,
        decimal grossAmount)
    {
        var gross = grossAmount.ToString("0.00", CultureInfo.InvariantCulture);
        var signature = Convert.ToHexString(
                SHA512.HashData(
                    Encoding.UTF8.GetBytes(
                        $"{providerOrderId}200{gross}{AuthWebApplicationFactory.TestMidtransServerKey}")))
            .ToLowerInvariant();

        return new Dictionary<string, object?>
        {
            ["transaction_time"] = "2026-11-08 12:00:00",
            ["transaction_status"] = transactionStatus,
            ["transaction_id"] = $"txn-{Guid.NewGuid():N}",
            ["status_message"] = "midtrans payment notification",
            ["status_code"] = "200",
            ["signature_key"] = signature,
            ["payment_type"] = "qris",
            ["order_id"] = providerOrderId,
            ["gross_amount"] = gross,
            ["fraud_status"] = "accept",
            ["currency"] = "IDR"
        };
    }

    private async Task<(OrderResponse Order, Guid CashierOrderId)> CreateConfirmedOrderAsync(
        HttpClient owner)
    {
        var product = await CreateCatalogItemAsync(owner, 30000m);
        var table = await CreateTableAsync(owner);
        var tableToken = await ResolveTableTokenAsync(table.QrUrl!);
        using var customer = factory.CreateClient();
        var order = await CreateOrderAsync(customer, tableToken, product.Id, 1, "cash");
        var cashierOrderId = await FindCashierOrderIdAsync(owner, order.OrderNumber);

        var confirm = await owner.PostAsJsonAsync(
            $"/api/v1/cashier/orders/{cashierOrderId}/confirm-cash",
            new { note = "Tunai diterima" });
        confirm.EnsureSuccessStatusCode();

        var detail = await confirm.Content.ReadFromJsonAsync<CashierOrderDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("processing", detail.Status);

        return (order, cashierOrderId);
    }

    private static async Task<Guid> FindCashierOrderIdAsync(
        HttpClient owner,
        string orderNumber)
    {
        var paged = await owner.GetFromJsonAsync<PagedCashierOrders>(
            "/api/v1/cashier/orders?pageSize=100");
        Assert.NotNull(paged);
        return paged.Items.Single(x => x.OrderNumber == orderNumber).Id;
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

    private static async Task<ProductResponse> CreateCatalogItemAsync(
        HttpClient owner,
        decimal price)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryResponse = await owner.PostAsJsonAsync("/api/v1/owner/categories", new
        {
            name = $"Test {suffix}",
            sortOrder = 1,
            isActive = true
        });
        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();

        var productResponse = await owner.PostAsJsonAsync("/api/v1/owner/products", new
        {
            name = $"Produk {suffix}",
            categoryId = category!.Id,
            description = "Produk uji order.",
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
            number = $"T{suffix}",
            name = $"Meja Test {suffix}",
            capacity = 2,
            isActive = true
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TableResponse>())!;
    }

    private async Task<string> ResolveTableTokenAsync(string qrUrl)
    {
        var token = ExtractQrToken(qrUrl);
        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<TableSessionResponse>(
            $"/api/v1/public/tables/{token}");
        Assert.NotNull(response);
        return response.TableSessionToken;
    }

    private static async Task<OrderResponse> CreateOrderAsync(
        HttpClient customer,
        string tableToken,
        Guid productId,
        short quantity,
        string paymentMethod,
        object? extraFields = null,
        string? idempotencyKey = null)
    {
        var response = await PostOrderAsync(
            customer, tableToken, productId, quantity, paymentMethod,
            idempotencyKey ?? Guid.NewGuid().ToString(), extraFields);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        return order;
    }

    private static async Task<HttpResponseMessage> PostOrderAsync(
        HttpClient customer,
        string tableToken,
        Guid productId,
        short quantity,
        string paymentMethod,
        string idempotencyKey,
        object? extraFields = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tableToken);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var body = new Dictionary<string, object?>
        {
            ["customerName"] = "Customer Test",
            ["paymentMethod"] = paymentMethod,
            ["notes"] = "Uji integrasi",
            ["items"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["productId"] = productId,
                    ["quantity"] = quantity
                }
            }
        };

        if (extraFields is not null)
        {
            foreach (var property in extraFields.GetType().GetProperties())
            {
                body[property.Name] = property.GetValue(extraFields);
            }
        }

        request.Content = JsonContent.Create(body);
        return await customer.SendAsync(request);
    }

    private static string ExtractQrToken(string qrUrl) =>
        qrUrl.Split("/t/", StringSplitOptions.RemoveEmptyEntries)[^1];

    private sealed record LoginResponse(string AccessToken);

    private sealed record CategoryResponse(Guid Id);

    private sealed record ProductResponse(Guid Id, string Name, decimal Price);

    private sealed record TableResponse(
        Guid Id,
        string Number,
        string Name,
        short Capacity,
        bool IsActive,
        string? QrUrl);

    private sealed record TableSessionResponse(
        TableInfoResponse Table,
        string TableSessionToken,
        int ExpiresIn);

    private sealed record TableInfoResponse(
        Guid Id,
        string Number,
        string Name,
        short Capacity);

    private sealed record PaymentResponse(
        string Method,
        string Provider,
        string Status,
        DateTimeOffset? PaidAt = null,
        string? SnapToken = null,
        string? RedirectUrl = null,
        bool Simulation = false);

    private sealed record WebhookMessageResponse(string Message);

    private sealed record OrderItemResponse(
        string ProductName,
        decimal UnitPrice,
        short Quantity,
        decimal Subtotal);

    private sealed record OrderResponse(
        string OrderNumber,
        string Status,
        string CustomerName,
        TableInfoResponse Table,
        IReadOnlyList<OrderItemResponse> Items,
        decimal Subtotal,
        decimal TaxRate,
        decimal TaxAmount,
        decimal DiscountAmount,
        decimal Total,
        string TrackingToken,
        PaymentResponse Payment,
        DateTimeOffset CreatedAt);

    private sealed record TrackingEventResponse(
        string Status,
        DateTimeOffset At,
        string? Note);

    private sealed record OrderTrackingResponse(
        string OrderNumber,
        string Status,
        string CustomerName,
        TableInfoResponse Table,
        IReadOnlyList<OrderItemResponse> Items,
        decimal Subtotal,
        decimal TaxRate,
        decimal TaxAmount,
        decimal DiscountAmount,
        decimal Total,
        PaymentResponse Payment,
        IReadOnlyList<TrackingEventResponse> Timeline,
        DateTimeOffset CreatedAt);

    private sealed record CashierOrderSummaryResponse(
        Guid Id,
        string OrderNumber,
        string CustomerName,
        TableInfoResponse Table,
        decimal Total,
        string Status,
        string PaymentMethod,
        string PaymentStatus,
        int ItemCount,
        DateTimeOffset CreatedAt);

    private sealed record PagedCashierOrders(
        IReadOnlyList<CashierOrderSummaryResponse> Items,
        int TotalItems);

    private sealed record CashierOrderDetailResponse(
        Guid Id,
        string OrderNumber,
        string Status,
        PaymentResponse Payment);

    private sealed record TransactionSummaryResponse(
        string OrderNumber,
        string Method,
        string Status,
        DateTimeOffset? PaidAt);

    private sealed record PagedTransactionsResponse(
        IReadOnlyList<TransactionSummaryResponse> Items,
        int TotalItems);
}
