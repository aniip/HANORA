using Hanora.Domain.Enums;

namespace Hanora.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;

    public Guid TableId { get; set; }
    public DiningTable Table { get; set; } = null!;

    public Guid? CashierId { get; set; }
    public User? Cashier { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; } = 10.00m;
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }

    public string? Notes { get; set; }
    public string TrackingTokenHash { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<TransactionHistory> Histories { get; set; } = [];
    public Income? Income { get; set; }
}

public sealed class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; set; }
    public short Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public PaymentProvider Provider { get; set; }
    public PaymentMethod Method { get; set; }
    public string ProviderOrderId { get; set; } = string.Empty;

    public string? SnapToken { get; set; }
    public string? RedirectUrl { get; set; }
    public string? TransactionId { get; set; }

    public decimal GrossAmount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? FraudStatus { get; set; }
    public bool NeedsReview { get; set; }
    public string? ReviewNote { get; set; }

    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RawResponse { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<PaymentNotification> Notifications { get; set; } = [];
    public Income? Income { get; set; }
}

public sealed class PaymentNotification
{
    public Guid Id { get; set; }
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public string NotificationHash { get; set; } = string.Empty;
    public string ProviderOrderId { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string TransactionStatus { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public string SignatureKey { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    public string RawPayload { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
}
