using System.Data;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hanora.Application.Abstractions;
using Hanora.Application.Common;
using Hanora.Application.Orders;
using Hanora.Application.Tables;
using Hanora.Domain.Entities;
using Hanora.Domain.Enums;
using Hanora.Infrastructure.Configuration;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hanora.Infrastructure.Services;

public sealed class OrderService(
    HanoraDbContext dbContext,
    JwtOptions jwtOptions,
    TimeProvider timeProvider) : IOrderService
{
    private static readonly TimeZoneInfo JakartaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    public async Task<ServiceResult<OrderResponseDto>> CreateAsync(
        Guid tableId,
        Guid qrCodeId,
        string tableNumber,
        string idempotencyKey,
        CreateOrderRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 64)
        {
            return ServiceResult<OrderResponseDto>.Failure(
                "idempotency_key_required",
                "Header Idempotency-Key wajib diisi (maksimal 64 karakter).");
        }

        var existing = await dbContext.Orders
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(
                x => x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.TableId != tableId)
            {
                return ServiceResult<OrderResponseDto>.Failure(
                    "idempotency_conflict",
                    "Idempotency-Key sudah digunakan untuk pesanan lain.");
            }

            var replayToken = GenerateTrackingToken(existing.Id, idempotencyKey);
            var replayHash = HashTrackingToken(replayToken);

            if (!string.Equals(existing.TrackingTokenHash, replayHash, StringComparison.Ordinal))
            {
                existing.TrackingTokenHash = replayHash;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return ServiceResult<OrderResponseDto>.Success(
                MapOrderResponse(existing, replayToken));
        }

        var qrCode = await dbContext.TableQrCodes
            .Include(x => x.Table)
            .FirstOrDefaultAsync(
                x => x.Id == qrCodeId &&
                     x.TableId == tableId &&
                     x.IsActive &&
                     x.RevokedAt == null,
                cancellationToken);

        if (qrCode is null || !qrCode.Table.IsActive)
        {
            return ServiceResult<OrderResponseDto>.Failure(
                "table_session_invalid",
                "QR meja sudah tidak berlaku. Silakan scan ulang QR di meja.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return ServiceResult<OrderResponseDto>.Failure(
                "validation", "Minimal satu item harus dipesan.");
        }

        var duplicateProduct = request.Items
            .GroupBy(x => x.ProductId)
            .Any(x => x.Count() > 1);

        if (duplicateProduct)
        {
            return ServiceResult<OrderResponseDto>.Failure(
                "validation", "Produk yang sama tidak boleh muncul lebih dari sekali.");
        }

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await dbContext.Products
            .Include(x => x.Category)
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var unavailable = new List<string>();
        var orderItems = new List<OrderItem>();
        decimal subtotal = 0m;

        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) ||
                product.ArchivedAt is not null ||
                !product.IsAvailable ||
                !product.Category.IsActive)
            {
                unavailable.Add(
                    product?.Name ?? item.ProductId.ToString());
                continue;
            }

            var lineSubtotal = product.Price * item.Quantity;
            subtotal += lineSubtotal;

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                UnitPriceSnapshot = product.Price,
                Quantity = item.Quantity,
                Subtotal = lineSubtotal,
                Notes = NormalizeOptional(item.Notes),
                CreatedAt = timeProvider.GetUtcNow()
            });
        }

        if (unavailable.Count > 0)
        {
            return ServiceResult<OrderResponseDto>.Failure(
                "product_unavailable",
                $"Produk berikut tidak tersedia: {string.Join(", ", unavailable)}.");
        }

        if (subtotal <= 0)
        {
            return ServiceResult<OrderResponseDto>.Failure(
                "validation", "Total pesanan tidak valid.");
        }

        var taxRate = await ReadDecimalSettingAsync("tax_rate", 10.00m, cancellationToken);
        if (taxRate is < 0 or > 100)
        {
            taxRate = 10.00m;
        }

        var taxAmount = Math.Round(
            subtotal * taxRate / 100m,
            0,
            MidpointRounding.AwayFromZero);
        var total = subtotal + taxAmount;

        var method = request.PaymentMethod.Trim().ToLowerInvariant();
        var paymentMethod = method == "qris" ? PaymentMethod.Qris : PaymentMethod.Cash;
        var paymentProvider = method == "qris" ? PaymentProvider.Midtrans : PaymentProvider.Manual;
        var initialStatus = method == "qris"
            ? OrderStatus.PendingPayment
            : OrderStatus.PendingConfirmation;

        var now = timeProvider.GetUtcNow();
        var orderId = Guid.NewGuid();
        var trackingToken = GenerateTrackingToken(orderId, idempotencyKey);
        var trackingHash = HashTrackingToken(trackingToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var dailyNumber = await NextDailyNumberAsync(cancellationToken);
            var day = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now, JakartaTimeZone).DateTime);
            var orderNumber = $"HNR-{day:yyyyMMdd}-{dailyNumber:0000}";

            var order = new Order
            {
                Id = orderId,
                OrderNumber = orderNumber,
                TableId = tableId,
                Table = qrCode.Table,
                CustomerName = request.CustomerName.Trim(),
                Status = initialStatus,
                Subtotal = subtotal,
                TaxRate = taxRate,
                TaxAmount = taxAmount,
                DiscountAmount = 0m,
                Total = total,
                Notes = NormalizeOptional(request.Notes),
                TrackingTokenHash = trackingHash,
                IdempotencyKey = idempotencyKey,
                CreatedAt = now,
                UpdatedAt = now
            };

            foreach (var item in orderItems)
            {
                item.OrderId = order.Id;
                order.Items.Add(item);
            }

            var paymentExpiryMinutes = await ReadIntSettingAsync(
                "payment_expiry_minutes", 15, cancellationToken);
            paymentExpiryMinutes = Math.Clamp(paymentExpiryMinutes, 5, 120);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Provider = paymentProvider,
                Method = paymentMethod,
                ProviderOrderId = $"{orderNumber}-P1",
                GrossAmount = total,
                Status = PaymentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = method == "qris"
                    ? now.AddMinutes(paymentExpiryMinutes)
                    : null
            };

            order.Payments.Add(payment);
            order.Histories.Add(new TransactionHistory
            {
                Id = 0,
                OrderId = order.Id,
                FromStatus = null,
                ToStatus = initialStatus,
                ActorType = ActorType.Customer,
                Note = "Pesanan dibuat customer",
                CreatedAt = now
            });

            dbContext.Orders.Add(order);
            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "order.created",
                Entity = "order",
                EntityId = order.Id.ToString(),
                Metadata = JsonSerializer.Serialize(new
                {
                    order.OrderNumber,
                    tableNumber,
                    total,
                    method
                }),
                Ip = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
                CreatedAt = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult<OrderResponseDto>.Success(
                MapOrderResponse(order, trackingToken));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            var duplicate = await dbContext.Orders
                .Include(x => x.Table)
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(
                    x => x.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (duplicate is not null)
            {
                var replayToken = GenerateTrackingToken(duplicate.Id, idempotencyKey);
                return ServiceResult<OrderResponseDto>.Success(
                    MapOrderResponse(duplicate, replayToken));
            }

            throw;
        }
    }

    public async Task<OrderTrackingDto?> GetTrackingAsync(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return null;
        }

        var hash = HashTrackingToken(trackingToken);
        var order = await LoadOrderForTrackingAsync(hash, cancellationToken);
        return order is null ? null : MapOrderTracking(order);
    }

    public async Task<ServiceResult<OrderTrackingDto>> CancelAsync(
        string trackingToken,
        string? reason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var hash = HashTrackingToken(trackingToken);
        var order = await dbContext.Orders
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.TrackingTokenHash == hash, cancellationToken);

        if (order is null)
        {
            return ServiceResult<OrderTrackingDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.");
        }

        if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.PendingConfirmation))
        {
            return ServiceResult<OrderTrackingDto>.Failure(
                "invalid_status",
                "Pesanan sudah diproses dan tidak dapat dibatalkan dari halaman ini.");
        }

        var now = timeProvider.GetUtcNow();
        var previousStatus = order.Status;

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = now;
        order.UpdatedAt = now;
        order.CancelReason = NormalizeOptional(reason) ?? "Dibatalkan customer";

        foreach (var payment in order.Payments.Where(x => x.Status == PaymentStatus.Pending))
        {
            payment.Status = PaymentStatus.Cancelled;
            payment.UpdatedAt = now;
        }

        order.Histories.Add(new TransactionHistory
        {
            Id = 0,
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Cancelled,
            ActorType = ActorType.Customer,
            Note = order.CancelReason,
            CreatedAt = now
        });

        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "order.cancelled_by_customer",
            Entity = "order",
            EntityId = order.Id.ToString(),
            Metadata = JsonSerializer.Serialize(new { order.OrderNumber, reason }),
            Ip = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<OrderTrackingDto>.Success(MapOrderTracking(order));
    }

    public async Task<PagedResult<CashierOrderSummaryDto>> GetCashierOrdersAsync(
        string? status,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .AsQueryable();

        var parsedStatus = ParseOrderStatus(status);
        if (parsedStatus is not null)
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (from is not null)
        {
            var start = new DateTimeOffset(
                from.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(7));
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (to is not null)
        {
            var end = new DateTimeOffset(
                to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(7));
            query = query.Where(x => x.CreatedAt < end);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CashierOrderSummaryDto>(
            orders.Select(MapCashierSummary).ToList(),
            safePage,
            safePageSize,
            totalItems);
    }

    public async Task<CashierOrderDetailDto?> GetCashierOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.Histories)
            .Include(x => x.Cashier)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        return order is null ? null : MapCashierDetail(order);
    }

    public async Task<ServiceResult<CashierOrderDetailDto>> UpdateStatusAsync(
        Guid orderId,
        UpdateOrderStatusRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var order = await LoadCashierOrderForUpdateAsync(orderId, cancellationToken);

        if (order is null)
        {
            return ServiceResult<CashierOrderDetailDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.");
        }

        var target = request.ToStatus.Trim().ToLowerInvariant();
        var targetStatus = (order.Status, target) switch
        {
            (OrderStatus.Processing, "ready") => OrderStatus.Ready,
            (OrderStatus.Ready, "completed") => OrderStatus.Completed,
            _ => (OrderStatus?)null
        };

        if (targetStatus is null)
        {
            return ServiceResult<CashierOrderDetailDto>.Failure(
                "invalid_status_transition",
                "Transisi status tidak diizinkan.");
        }

        var now = timeProvider.GetUtcNow();
        var previousStatus = order.Status;
        order.Status = targetStatus.Value;
        order.UpdatedAt = now;
        order.CashierId = actorId;
        await AttachCashierAsync(order, actorId, cancellationToken);

        if (targetStatus == OrderStatus.Completed)
        {
            order.CompletedAt = now;
        }

        order.Histories.Add(new TransactionHistory
        {
            Id = 0,
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = targetStatus.Value,
            ActorType = ActorType.Cashier,
            ChangedBy = actorId,
            Note = NormalizeOptional(request.Note),
            CreatedAt = now
        });

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "order.status_updated",
            Entity = "order",
            EntityId = order.Id.ToString(),
            Metadata = JsonSerializer.Serialize(new
            {
                order.OrderNumber,
                from = StatusToString(previousStatus),
                to = StatusToString(targetStatus.Value)
            }),
            Ip = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<CashierOrderDetailDto>.Success(MapCashierDetail(order));
    }

    public async Task<ServiceResult<CashierOrderDetailDto>> ConfirmCashAsync(
        Guid orderId,
        ConfirmCashRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var order = await LoadCashierOrderForUpdateAsync(orderId, cancellationToken);

        if (order is null)
        {
            return ServiceResult<CashierOrderDetailDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.");
        }

        if (order.Status != OrderStatus.PendingConfirmation)
        {
            return ServiceResult<CashierOrderDetailDto>.Failure(
                "invalid_status_transition",
                "Pesanan tidak berada pada status Menunggu Konfirmasi.");
        }

        var payment = order.Payments
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault(x => x.Method == PaymentMethod.Cash);

        if (payment is null || payment.Status != PaymentStatus.Pending)
        {
            return ServiceResult<CashierOrderDetailDto>.Failure(
                "payment_not_payable",
                "Pembayaran tunai tidak dapat dikonfirmasi.");
        }

        var now = timeProvider.GetUtcNow();
        var previousStatus = order.Status;

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = now;
        payment.UpdatedAt = now;

        order.Status = OrderStatus.Processing;
        order.PaidAt = now;
        order.UpdatedAt = now;
        order.CashierId = actorId;
        await AttachCashierAsync(order, actorId, cancellationToken);

        order.Histories.Add(new TransactionHistory
        {
            Id = 0,
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Processing,
            ActorType = ActorType.Cashier,
            ChangedBy = actorId,
            Note = NormalizeOptional(request.Note) ?? "Pembayaran tunai dikonfirmasi",
            CreatedAt = now
        });

        if (!await dbContext.Incomes.AnyAsync(
                x => x.OrderId == order.Id,
                cancellationToken))
        {
            var occurredAt = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now, JakartaTimeZone).DateTime);

            dbContext.Incomes.Add(new Income
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PaymentId = payment.Id,
                Source = "order",
                Description = $"Pembayaran {order.OrderNumber}",
                Amount = order.Total,
                OccurredAt = occurredAt,
                CreatedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "order.cash_confirmed",
            Entity = "order",
            EntityId = order.Id.ToString(),
            Metadata = JsonSerializer.Serialize(new
            {
                order.OrderNumber,
                amount = order.Total
            }),
            Ip = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<CashierOrderDetailDto>.Success(MapCashierDetail(order));
    }

    public async Task<ServiceResult<CashierOrderDetailDto>> CancelByCashierAsync(
        Guid orderId,
        CancelCashierOrderRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var order = await LoadCashierOrderForUpdateAsync(orderId, cancellationToken);

        if (order is null)
        {
            return ServiceResult<CashierOrderDetailDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.");
        }

        if (order.Status is not (OrderStatus.PendingPayment or OrderStatus.PendingConfirmation))
        {
            return ServiceResult<CashierOrderDetailDto>.Failure(
                "order_not_cancellable",
                "Pesanan yang sudah diproses tidak dapat dibatalkan dari dashboard kasir.");
        }

        var now = timeProvider.GetUtcNow();
        var previousStatus = order.Status;
        var reason = request.Reason.Trim();

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = now;
        order.CancelReason = reason;
        order.UpdatedAt = now;
        order.CashierId = actorId;
        await AttachCashierAsync(order, actorId, cancellationToken);

        foreach (var payment in order.Payments.Where(x => x.Status == PaymentStatus.Pending))
        {
            payment.Status = PaymentStatus.Cancelled;
            payment.UpdatedAt = now;
        }

        order.Histories.Add(new TransactionHistory
        {
            Id = 0,
            OrderId = order.Id,
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Cancelled,
            ActorType = ActorType.Cashier,
            ChangedBy = actorId,
            Note = reason,
            CreatedAt = now
        });

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = actorId,
            Action = "order.cancelled_by_cashier",
            Entity = "order",
            EntityId = order.Id.ToString(),
            Metadata = JsonSerializer.Serialize(new { order.OrderNumber, reason }),
            Ip = IPAddress.TryParse(ipAddress, out var ip) ? ip : null,
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<CashierOrderDetailDto>.Success(MapCashierDetail(order));
    }

    public async Task<PagedResult<TransactionSummaryDto>> GetTransactionsAsync(
        DateOnly? from,
        DateOnly? to,
        string? method,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Payments
            .AsNoTracking()
            .Include(x => x.Order)
            .ThenInclude(x => x.Table)
            .Where(x => x.Status == PaymentStatus.Paid ||
                        x.Status == PaymentStatus.Refunded)
            .AsQueryable();

        var parsedMethod = ParsePaymentMethod(method);
        if (parsedMethod is not null)
        {
            query = query.Where(x => x.Method == parsedMethod);
        }

        var parsedStatus = ParsePaymentStatus(status);
        if (parsedStatus is not null)
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (from is not null)
        {
            var start = new DateTimeOffset(
                from.Value.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(7));
            query = query.Where(x => x.PaidAt != null && x.PaidAt >= start);
        }

        if (to is not null)
        {
            var end = new DateTimeOffset(
                to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(7));
            query = query.Where(x => x.PaidAt != null && x.PaidAt < end);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var payments = await query
            .OrderByDescending(x => x.PaidAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var items = payments.Select(x => new TransactionSummaryDto(
            x.Order.OrderNumber,
            x.Order.CustomerName,
            MapTableInfo(x.Order.Table),
            x.Order.Total,
            PaymentMethodToString(x.Method),
            PaymentStatusToString(x.Status),
            x.PaidAt)).ToList();

        return new PagedResult<TransactionSummaryDto>(
            items, safePage, safePageSize, totalItems);
    }

    private async Task<Order?> LoadCashierOrderForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken) =>
        await dbContext.Orders
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

    private async Task AttachCashierAsync(
        Order order,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var cashier = await dbContext.Users
            .FindAsync(new object[] { actorId }, cancellationToken);

        if (cashier is not null)
        {
            order.Cashier = cashier;
        }
    }

    private async Task<Order?> LoadOrderForTrackingAsync(
        string trackingHash,
        CancellationToken cancellationToken) =>
        await dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(
                x => x.TrackingTokenHash == trackingHash,
                cancellationToken);

    private async Task<int> NextDailyNumberAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                INSERT INTO daily_counters (day, last_number)
                VALUES ((now() AT TIME ZONE 'Asia/Jakarta')::date, 1)
                ON CONFLICT (day)
                DO UPDATE SET last_number = daily_counters.last_number + 1
                RETURNING last_number;
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<decimal> ReadDecimalSettingAsync(
        string key,
        decimal fallback,
        CancellationToken cancellationToken)
    {
        var raw = await dbContext.Settings
            .AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<decimal>(raw);
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private async Task<int> ReadIntSettingAsync(
        string key,
        int fallback,
        CancellationToken cancellationToken)
    {
        var raw = await dbContext.Settings
            .AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<int>(raw);
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private OrderResponseDto MapOrderResponse(Order order, string trackingToken)
    {
        var payment = LatestPayment(order);

        return new OrderResponseDto(
            order.OrderNumber,
            StatusToString(order.Status),
            order.CustomerName,
            MapTableInfo(order.Table),
            MapItems(order.Items),
            order.Subtotal,
            order.TaxRate,
            order.TaxAmount,
            order.DiscountAmount,
            order.Total,
            trackingToken,
            MapPayment(payment),
            order.CreatedAt);
    }

    private static OrderTrackingDto MapOrderTracking(Order order)
    {
        var payment = LatestPayment(order);

        return new OrderTrackingDto(
            order.OrderNumber,
            StatusToString(order.Status),
            order.CustomerName,
            MapTableInfo(order.Table),
            MapItems(order.Items),
            order.Subtotal,
            order.TaxRate,
            order.TaxAmount,
            order.DiscountAmount,
            order.Total,
            MapPayment(payment),
            order.Histories
                .OrderBy(x => x.CreatedAt)
                .Select(x => new TrackingEventDto(
                    StatusToString(x.ToStatus),
                    x.CreatedAt,
                    x.Note))
                .ToList(),
            order.CreatedAt);
    }

    private static CashierOrderSummaryDto MapCashierSummary(Order order)
    {
        var payment = LatestPayment(order);

        return new CashierOrderSummaryDto(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            MapTableInfo(order.Table),
            order.Total,
            StatusToString(order.Status),
            PaymentMethodToString(payment?.Method),
            PaymentStatusToString(payment?.Status),
            order.Items.Sum(x => (int)x.Quantity),
            order.CreatedAt);
    }

    private static CashierOrderDetailDto MapCashierDetail(Order order)
    {
        var payment = LatestPayment(order);

        return new CashierOrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            MapTableInfo(order.Table),
            StatusToString(order.Status),
            MapItems(order.Items),
            order.Subtotal,
            order.TaxRate,
            order.TaxAmount,
            order.DiscountAmount,
            order.Total,
            order.Notes,
            order.Cashier?.Name,
            order.PaidAt,
            order.CreatedAt,
            MapPayment(payment),
            order.Histories
                .OrderBy(x => x.CreatedAt)
                .Select(x => new TrackingEventDto(
                    StatusToString(x.ToStatus),
                    x.CreatedAt,
                    x.Note))
                .ToList());
    }

    private static Payment? LatestPayment(Order order) =>
        order.Payments
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

    private static IReadOnlyList<OrderItemDto> MapItems(IEnumerable<OrderItem> items) =>
        items
            .OrderBy(x => x.CreatedAt)
            .Select(x => new OrderItemDto(
                x.ProductNameSnapshot,
                x.UnitPriceSnapshot,
                x.Quantity,
                x.Subtotal,
                x.Notes))
            .ToList();

    private static TableInfoDto MapTableInfo(DiningTable table) =>
        new(table.Id, table.Number, table.Name, table.Capacity);

    private static PaymentSummaryDto MapPayment(Payment? payment) =>
        payment is null
            ? new PaymentSummaryDto("unknown", "unknown", "pending", null, null)
            : new PaymentSummaryDto(
                PaymentMethodToString(payment.Method),
                payment.Provider == PaymentProvider.Midtrans ? "midtrans" : "manual",
                PaymentStatusToString(payment.Status),
                payment.PaidAt,
                payment.ExpiresAt,
                payment.SnapToken,
                payment.RedirectUrl,
                payment.SnapToken?.StartsWith("SIM-", StringComparison.Ordinal) == true);

    private string GenerateTrackingToken(Guid orderId, string idempotencyKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var bytes = hmac.ComputeHash(
            Encoding.UTF8.GetBytes($"tracking:{orderId:N}:{idempotencyKey}"));
        return Base64UrlEncode(bytes);
    }

    private static string HashTrackingToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OrderStatus? ParseOrderStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "pending_payment" => OrderStatus.PendingPayment,
            "pending_confirmation" => OrderStatus.PendingConfirmation,
            "processing" => OrderStatus.Processing,
            "ready" => OrderStatus.Ready,
            "completed" => OrderStatus.Completed,
            "cancelled" => OrderStatus.Cancelled,
            _ => null
        };

    private static PaymentMethod? ParsePaymentMethod(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "qris" => PaymentMethod.Qris,
            "cash" => PaymentMethod.Cash,
            _ => null
        };

    private static PaymentStatus? ParsePaymentStatus(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "pending" => PaymentStatus.Pending,
            "paid" => PaymentStatus.Paid,
            "failed" => PaymentStatus.Failed,
            "expired" => PaymentStatus.Expired,
            "refunded" => PaymentStatus.Refunded,
            "cancelled" => PaymentStatus.Cancelled,
            _ => null
        };

    private static string StatusToString(OrderStatus status) =>
        status switch
        {
            OrderStatus.PendingPayment => "pending_payment",
            OrderStatus.PendingConfirmation => "pending_confirmation",
            OrderStatus.Processing => "processing",
            OrderStatus.Ready => "ready",
            OrderStatus.Completed => "completed",
            OrderStatus.Cancelled => "cancelled",
            _ => "unknown"
        };

    private static string PaymentStatusToString(PaymentStatus? status) =>
        status switch
        {
            PaymentStatus.Pending => "pending",
            PaymentStatus.Paid => "paid",
            PaymentStatus.Failed => "failed",
            PaymentStatus.Expired => "expired",
            PaymentStatus.Refunded => "refunded",
            PaymentStatus.Cancelled => "cancelled",
            _ => "unknown"
        };

    private static string PaymentMethodToString(PaymentMethod? method) =>
        method switch
        {
            PaymentMethod.Qris => "qris",
            PaymentMethod.Cash => "cash",
            _ => "unknown"
        };
}
