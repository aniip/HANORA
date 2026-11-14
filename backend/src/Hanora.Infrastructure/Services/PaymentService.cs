using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hanora.Application.Abstractions;
using Hanora.Application.Common;
using Hanora.Application.Orders;
using Hanora.Application.Payments;
using Hanora.Domain.Entities;
using Hanora.Domain.Enums;
using Hanora.Infrastructure.Configuration;
using Hanora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hanora.Infrastructure.Services;

/// <summary>
/// Menangani pembayaran QRIS Midtrans: inisiasi Snap, mode simulasi lokal,
/// dan notifikasi webhook dengan validasi signature serta idempotensi.
/// </summary>
public sealed class PaymentService(
    HanoraDbContext dbContext,
    MidtransOptions options,
    AppOptions appOptions,
    IMidtransClient midtransClient,
    IOrderService orderService,
    TimeProvider timeProvider,
    ILogger<PaymentService> logger) : IPaymentService
{
    private static readonly TimeZoneInfo JakartaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    public async Task<ServiceResult<PaymentSummaryDto>> InitiateQrisAsync(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        if (!options.QrisEnabled)
        {
            return ServiceResult<PaymentSummaryDto>.Failure(
                "qris_disabled",
                "Pembayaran QRIS sedang dinonaktifkan.");
        }

        if (string.IsNullOrWhiteSpace(trackingToken))
        {
            return ServiceResult<PaymentSummaryDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.");
        }

        var hash = HashTrackingToken(trackingToken);
        var order = await dbContext.Orders
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(
                x => x.TrackingTokenHash == hash,
                cancellationToken);

        if (order is null)
        {
            return ServiceResult<PaymentSummaryDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.");
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed)
        {
            return ServiceResult<PaymentSummaryDto>.Failure(
                "invalid_status",
                "Pesanan ini tidak dapat dibayar lagi.");
        }

        var payment = await ResolvePayableQrisPaymentAsync(
            order,
            cancellationToken);

        if (payment.Status == PaymentStatus.Paid)
        {
            return ServiceResult<PaymentSummaryDto>.Success(MapPayment(payment));
        }

        var now = timeProvider.GetUtcNow();

        if (options.IsSimulation)
        {
            if (string.IsNullOrWhiteSpace(payment.SnapToken))
            {
                payment.SnapToken = $"SIM-{Guid.NewGuid():N}";
                payment.UpdatedAt = now;
            }

            payment.RedirectUrl = $"/pesanan/{trackingToken}/simulasi";
            payment.RawResponse = JsonSerializer.Serialize(new
            {
                mode = "simulation",
                providerOrderId = payment.ProviderOrderId,
                grossAmount = payment.GrossAmount
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<PaymentSummaryDto>.Success(MapPayment(payment));
        }

        // Pembayaran pending yang masih memiliki token Snap aktif dapat dipakai ulang.
        if (!string.IsNullOrWhiteSpace(payment.SnapToken) &&
            !string.IsNullOrWhiteSpace(payment.RedirectUrl) &&
            payment.ExpiresAt > now)
        {
            return ServiceResult<PaymentSummaryDto>.Success(MapPayment(payment));
        }

        try
        {
            var snap = await midtransClient.CreateSnapTransactionAsync(
                new SnapTransactionRequest(
                    payment.ProviderOrderId,
                    payment.GrossAmount,
                    order.CustomerName,
                    $"{appOptions.BaseUrl}/pesanan/{trackingToken}"),
                cancellationToken);

            payment.SnapToken = snap.Token;
            payment.RedirectUrl = snap.RedirectUrl;
            payment.RawResponse = JsonSerializer.Serialize(new
            {
                mode = options.Mode,
                providerOrderId = payment.ProviderOrderId,
                token = snap.Token
            });
            payment.UpdatedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<PaymentSummaryDto>.Success(MapPayment(payment));
        }
        catch (MidtransException exception)
        {
            logger.LogError(
                exception,
                "Gagal membuat transaksi QRIS untuk {OrderNumber}",
                order.OrderNumber);

            return ServiceResult<PaymentSummaryDto>.Failure(
                "provider_error",
                "Gagal menghubungi Midtrans. Silakan coba lagi.");
        }
    }

    public async Task<ServiceResult<OrderTrackingDto>> SimulateAsync(
        string trackingToken,
        CancellationToken cancellationToken)
    {
        if (!options.IsSimulation)
        {
            return ServiceResult<OrderTrackingDto>.Failure(
                "simulation_disabled",
                "Simulasi pembayaran hanya tersedia pada mode simulation.");
        }

        var hash = HashTrackingToken(trackingToken);
        var order = await dbContext.Orders
            .Include(x => x.Payments)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(
                x => x.TrackingTokenHash == hash,
                cancellationToken);

        if (order is null)
        {
            return ServiceResult<OrderTrackingDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.");
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed)
        {
            return ServiceResult<OrderTrackingDto>.Failure(
                "invalid_status",
                "Pesanan ini tidak dapat dibayar lagi.");
        }

        var payment = LatestQrisPayment(order);

        if (payment is null)
        {
            return ServiceResult<OrderTrackingDto>.Failure(
                "payment_not_payable",
                "Pesanan ini tidak memiliki pembayaran QRIS.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            var existing = await orderService.GetTrackingAsync(
                trackingToken, cancellationToken);

            return existing is null
                ? ServiceResult<OrderTrackingDto>.Failure(
                    "not_found", "Pesanan tidak ditemukan.")
                : ServiceResult<OrderTrackingDto>.Success(existing);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return ServiceResult<OrderTrackingDto>.Failure(
                "payment_not_payable",
                "Pembayaran QRIS tidak berada pada status menunggu.");
        }

        var now = timeProvider.GetUtcNow();

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = now;
        payment.TransactionId = $"SIM-{Guid.NewGuid():N}";
        payment.FraudStatus = "accept";
        payment.UpdatedAt = now;

        if (order.Status == OrderStatus.PendingPayment)
        {
            var previousStatus = order.Status;
            order.Status = OrderStatus.Processing;
            order.PaidAt = now;
            order.UpdatedAt = now;

            order.Histories.Add(new TransactionHistory
            {
                Id = 0,
                OrderId = order.Id,
                FromStatus = previousStatus,
                ToStatus = OrderStatus.Processing,
                ActorType = ActorType.System,
                Note = "Pembayaran QRIS disimulasikan",
                CreatedAt = now
            });
        }

        await EnsureIncomeAsync(order, payment, now, cancellationToken);

        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "payment.qris_simulated",
            Entity = "order",
            EntityId = order.Id.ToString(),
            Metadata = JsonSerializer.Serialize(new
            {
                order.OrderNumber,
                amount = order.Total
            }),
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var tracking = await orderService.GetTrackingAsync(
            trackingToken, cancellationToken);

        return tracking is null
            ? ServiceResult<OrderTrackingDto>.Failure(
                "not_found", "Pesanan tidak ditemukan.")
            : ServiceResult<OrderTrackingDto>.Success(tracking);
    }

    public async Task<WebhookResult> HandleWebhookAsync(
        MidtransNotificationRequest notification,
        string rawBody,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var signatureValid = midtransClient.VerifySignature(
            notification.OrderId,
            notification.StatusCode,
            notification.GrossAmount,
            notification.SignatureKey);

        var notificationHash = HashNotification(notification);

        var alreadyHandled = await dbContext.PaymentNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.NotificationHash == notificationHash,
                cancellationToken);

        if (alreadyHandled is not null)
        {
            var duplicateStatus =
                alreadyHandled.ProcessingError == "invalid_signature" ? 401 : 200;

            return new WebhookResult(
                duplicateStatus,
                alreadyHandled.ProcessingError ?? "duplicate_notification");
        }

        var payment = await dbContext.Payments
            .Include(x => x.Order)
            .ThenInclude(x => x.Histories)
            .FirstOrDefaultAsync(
                x => x.ProviderOrderId == notification.OrderId,
                cancellationToken);

        var record = new PaymentNotification
        {
            Id = Guid.NewGuid(),
            PaymentId = payment?.Id,
            NotificationHash = notificationHash,
            ProviderOrderId = notification.OrderId,
            TransactionId = notification.TransactionId,
            TransactionStatus = notification.TransactionStatus,
            StatusCode = notification.StatusCode,
            GrossAmount = ParseAmount(notification.GrossAmount),
            SignatureKey = notification.SignatureKey,
            SignatureValid = signatureValid,
            RawPayload = string.IsNullOrWhiteSpace(rawBody)
                ? JsonSerializer.Serialize(notification)
                : rawBody,
            ReceivedAt = now
        };

        dbContext.PaymentNotifications.Add(record);

        if (!signatureValid)
        {
            record.ProcessingError = "invalid_signature";
            record.ProcessedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Webhook Midtrans dengan signature tidak valid untuk {OrderId}",
                notification.OrderId);

            return new WebhookResult(401, "invalid_signature");
        }

        if (payment is null)
        {
            record.ProcessingError = "payment_not_found";
            record.ProcessedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Webhook Midtrans untuk pembayaran yang tidak dikenal: {OrderId}",
                notification.OrderId);

            return new WebhookResult(200, "payment_not_found");
        }

        if (record.GrossAmount != payment.GrossAmount)
        {
            record.ProcessingError = "amount_mismatch";
            record.ProcessedAt = now;

            payment.NeedsReview = true;
            payment.ReviewNote = "Nominal notifikasi tidak sesuai dengan nominal pesanan.";
            payment.UpdatedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Nominal webhook Midtrans tidak cocok untuk {OrderId}: {Received} vs {Expected}",
                notification.OrderId,
                record.GrossAmount,
                payment.GrossAmount);

            return new WebhookResult(200, "amount_mismatch");
        }

        try
        {
            await ApplyNotificationAsync(
                payment,
                notification,
                record,
                now,
                cancellationToken);

            record.ProcessedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new WebhookResult(200, "ok");
        }
        catch (DbUpdateException exception)
        {
            // Dua notifikasi identik yang datang bersamaan: unique index pada
            // notification_hash membuat salah satunya gagal. Anggap duplikat.
            dbContext.ChangeTracker.Clear();

            logger.LogInformation(
                exception,
                "Webhook Midtrans duplikat terdeteksi untuk {OrderId}",
                notification.OrderId);

            return new WebhookResult(200, "duplicate_notification");
        }
    }

    private async Task<Payment> ResolvePayableQrisPaymentAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var latest = LatestQrisPayment(order);

        if (latest is not null &&
            latest.Status == PaymentStatus.Pending &&
            (latest.ExpiresAt is null || latest.ExpiresAt > now))
        {
            return latest;
        }

        if (latest is not null && latest.Status == PaymentStatus.Pending)
        {
            latest.Status = PaymentStatus.Expired;
            latest.UpdatedAt = now;
        }

        if (latest is not null && latest.Status == PaymentStatus.Paid)
        {
            return latest;
        }

        var expiryMinutes = await ReadIntSettingAsync(
            "payment_expiry_minutes", 15, cancellationToken);
        expiryMinutes = Math.Clamp(expiryMinutes, 5, 120);

        var sequence = order.Payments.Count + 1;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Provider = PaymentProvider.Midtrans,
            Method = PaymentMethod.Qris,
            ProviderOrderId = $"{order.OrderNumber}-P{sequence}",
            GrossAmount = order.Total,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddMinutes(expiryMinutes)
        };

        order.Payments.Add(payment);
        return payment;
    }

    private async Task ApplyNotificationAsync(
        Payment payment,
        MidtransNotificationRequest notification,
        PaymentNotification record,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var status = notification.TransactionStatus
            .Trim()
            .ToLowerInvariant();

        payment.TransactionId = notification.TransactionId;
        payment.FraudStatus = notification.FraudStatus;
        payment.RawResponse = record.RawPayload;
        payment.UpdatedAt = now;

        switch (status)
        {
            case "capture" when !string.Equals(
                notification.FraudStatus,
                "challenge",
                StringComparison.OrdinalIgnoreCase):
            case "settlement":
                await MarkPaidAsync(payment, notification, now, cancellationToken);
                break;

            case "capture":
                payment.NeedsReview = true;
                payment.ReviewNote = "Pembayaran ditandai challenge oleh Midtrans.";
                break;

            case "pending":
                break;

            case "deny":
            case "failure":
                if (payment.Status != PaymentStatus.Paid)
                {
                    payment.Status = PaymentStatus.Failed;
                }
                break;

            case "cancel":
                if (payment.Status != PaymentStatus.Paid)
                {
                    payment.Status = PaymentStatus.Cancelled;
                }
                break;

            case "expire":
                if (payment.Status != PaymentStatus.Paid)
                {
                    payment.Status = PaymentStatus.Expired;
                }
                break;

            case "refund":
            case "partial_refund":
                payment.Status = PaymentStatus.Refunded;
                break;

            case "chargeback":
                payment.Status = PaymentStatus.Refunded;
                payment.NeedsReview = true;
                payment.ReviewNote = "Chargeback diterima dari Midtrans.";
                break;

            default:
                record.ProcessingError = "unknown_transaction_status";
                break;
        }
    }

    private async Task MarkPaidAsync(
        Payment payment,
        MidtransNotificationRequest notification,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var order = payment.Order;

        if (payment.Status == PaymentStatus.Paid)
        {
            return;
        }

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = ParseTransactionTime(notification.TransactionTime) ?? now;

        if (order.Status == OrderStatus.PendingPayment)
        {
            var previousStatus = order.Status;
            order.Status = OrderStatus.Processing;
            order.PaidAt = payment.PaidAt;
            order.UpdatedAt = now;

            order.Histories.Add(new TransactionHistory
            {
                Id = 0,
                OrderId = order.Id,
                FromStatus = previousStatus,
                ToStatus = OrderStatus.Processing,
                ActorType = ActorType.System,
                Note = "Pembayaran QRIS diterima",
                CreatedAt = now
            });
        }
        else if (order.Status == OrderStatus.Cancelled)
        {
            payment.NeedsReview = true;
            payment.ReviewNote = "Pembayaran diterima setelah pesanan dibatalkan.";
            return;
        }

        await EnsureIncomeAsync(order, payment, now, cancellationToken);
    }

    private async Task EnsureIncomeAsync(
        Order order,
        Payment payment,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (order.Status == OrderStatus.Cancelled)
        {
            return;
        }

        if (await dbContext.Incomes.AnyAsync(
                x => x.OrderId == order.Id,
                cancellationToken))
        {
            return;
        }

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
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static Payment? LatestQrisPayment(Order order) =>
        order.Payments
            .Where(x => x.Method == PaymentMethod.Qris)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

    private static PaymentSummaryDto MapPayment(Payment payment) =>
        new(
            "qris",
            "midtrans",
            PaymentStatusToString(payment.Status),
            payment.PaidAt,
            payment.ExpiresAt,
            payment.SnapToken,
            payment.RedirectUrl,
            payment.SnapToken?.StartsWith("SIM-", StringComparison.Ordinal) == true);

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

    private static string HashTrackingToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();

    private static string HashNotification(MidtransNotificationRequest notification)
    {
        var raw = string.Join(
            '|',
            notification.OrderId,
            notification.StatusCode,
            notification.GrossAmount,
            notification.TransactionStatus,
            notification.SignatureKey);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
            .ToLowerInvariant();
    }

    private static decimal ParseAmount(string value) =>
        decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : 0m;

    private static DateTimeOffset? ParseTransactionTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            // Midtrans mengirim waktu dalam zona Asia/Jakarta. PostgreSQL
            // menyimpan timestamptz dalam UTC, jadi konversi dulu.
            return new DateTimeOffset(parsed, TimeSpan.FromHours(7))
                .ToUniversalTime();
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var fallback)
            ? fallback.ToUniversalTime()
            : null;
    }

    private static string PaymentStatusToString(PaymentStatus status) =>
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
}
