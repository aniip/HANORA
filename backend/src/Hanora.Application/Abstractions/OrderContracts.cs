using Hanora.Application.Common;
using Hanora.Application.Orders;

namespace Hanora.Application.Abstractions;

public interface IOrderService
{
    Task<ServiceResult<OrderResponseDto>> CreateAsync(
        Guid tableId,
        Guid qrCodeId,
        string tableNumber,
        string idempotencyKey,
        CreateOrderRequest request,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<OrderTrackingDto?> GetTrackingAsync(
        string trackingToken,
        CancellationToken cancellationToken);

    Task<ServiceResult<OrderTrackingDto>> CancelAsync(
        string trackingToken,
        string? reason,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<PagedResult<CashierOrderSummaryDto>> GetCashierOrdersAsync(
        string? status,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CashierOrderDetailDto?> GetCashierOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken);

    Task<ServiceResult<CashierOrderDetailDto>> UpdateStatusAsync(
        Guid orderId,
        UpdateOrderStatusRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<CashierOrderDetailDto>> ConfirmCashAsync(
        Guid orderId,
        ConfirmCashRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<ServiceResult<CashierOrderDetailDto>> CancelByCashierAsync(
        Guid orderId,
        CancelCashierOrderRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<PagedResult<TransactionSummaryDto>> GetTransactionsAsync(
        DateOnly? from,
        DateOnly? to,
        string? method,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
