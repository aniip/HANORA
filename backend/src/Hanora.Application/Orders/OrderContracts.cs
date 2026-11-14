using System.ComponentModel.DataAnnotations;
using Hanora.Application.Tables;

namespace Hanora.Application.Orders;

public sealed class CreateOrderRequest
{
    [Required(ErrorMessage = "Nama customer wajib diisi.")]
    [MinLength(2, ErrorMessage = "Nama customer minimal 2 karakter.")]
    [MaxLength(80)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Metode pembayaran wajib dipilih.")]
    [RegularExpression("^(qris|cash)$", ErrorMessage = "Metode pembayaran tidak valid.")]
    public string PaymentMethod { get; set; } = "cash";

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Minimal satu item harus dipesan.")]
    [MaxLength(50)]
    public List<CreateOrderItemRequest> Items { get; set; } = [];
}

public sealed class CreateOrderItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 99, ErrorMessage = "Jumlah item antara 1 dan 99.")]
    public short Quantity { get; set; }

    [MaxLength(255)]
    public string? Notes { get; set; }
}

public sealed record OrderItemDto(
    string ProductName,
    decimal UnitPrice,
    short Quantity,
    decimal Subtotal,
    string? Notes);

public sealed record PaymentSummaryDto(
    string Method,
    string Provider,
    string Status,
    DateTimeOffset? PaidAt,
    DateTimeOffset? ExpiresAt,
    string? SnapToken = null,
    string? RedirectUrl = null,
    bool Simulation = false);

public sealed record TrackingEventDto(
    string Status,
    DateTimeOffset At,
    string? Note);

public sealed record OrderResponseDto(
    string OrderNumber,
    string Status,
    string CustomerName,
    TableInfoDto Table,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal Total,
    string TrackingToken,
    PaymentSummaryDto Payment,
    DateTimeOffset CreatedAt);

public sealed record OrderTrackingDto(
    string OrderNumber,
    string Status,
    string CustomerName,
    TableInfoDto Table,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal Total,
    PaymentSummaryDto Payment,
    IReadOnlyList<TrackingEventDto> Timeline,
    DateTimeOffset CreatedAt);

public sealed class CancelOrderRequest
{
    [MaxLength(255)]
    public string? Reason { get; set; }
}

public sealed class UpdateOrderStatusRequest
{
    [Required(ErrorMessage = "Status tujuan wajib diisi.")]
    [RegularExpression("^(ready|completed)$", ErrorMessage = "Status tujuan tidak valid.")]
    public string ToStatus { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Note { get; set; }
}

public sealed class ConfirmCashRequest
{
    [MaxLength(255)]
    public string? Note { get; set; }
}

public sealed class CancelCashierOrderRequest
{
    [Required(ErrorMessage = "Alasan pembatalan wajib diisi.")]
    [MinLength(3, ErrorMessage = "Alasan minimal 3 karakter.")]
    [MaxLength(255)]
    public string Reason { get; set; } = string.Empty;
}

public sealed record TransactionSummaryDto(
    string OrderNumber,
    string CustomerName,
    TableInfoDto Table,
    decimal Total,
    string Method,
    string Status,
    DateTimeOffset? PaidAt);

public sealed record CashierOrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    TableInfoDto Table,
    decimal Total,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    int ItemCount,
    DateTimeOffset CreatedAt);

public sealed record CashierOrderDetailDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    TableInfoDto Table,
    string Status,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal Total,
    string? Notes,
    string? CashierName,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    PaymentSummaryDto Payment,
    IReadOnlyList<TrackingEventDto> Timeline);
