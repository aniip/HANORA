using System.Text.Json.Serialization;
using Hanora.Application.Common;
using Hanora.Application.Orders;

namespace Hanora.Application.Payments;

public sealed class MidtransNotificationRequest
{
    [JsonPropertyName("transaction_time")]
    public string? TransactionTime { get; set; }

    [JsonPropertyName("transaction_status")]
    public string TransactionStatus { get; set; } = string.Empty;

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; set; }

    [JsonPropertyName("status_code")]
    public string StatusCode { get; set; } = string.Empty;

    [JsonPropertyName("signature_key")]
    public string SignatureKey { get; set; } = string.Empty;

    [JsonPropertyName("payment_type")]
    public string? PaymentType { get; set; }

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("gross_amount")]
    public string GrossAmount { get; set; } = string.Empty;

    [JsonPropertyName("fraud_status")]
    public string? FraudStatus { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

public sealed record SnapTransactionRequest(
    string OrderId,
    decimal GrossAmount,
    string CustomerName,
    string FinishUrl);

public sealed record SnapTransactionResult(string Token, string RedirectUrl);

public sealed record WebhookResult(int StatusCode, string Message);

public interface IMidtransClient
{
    Task<SnapTransactionResult> CreateSnapTransactionAsync(
        SnapTransactionRequest request,
        CancellationToken cancellationToken);

    bool VerifySignature(
        string orderId,
        string statusCode,
        string grossAmount,
        string signatureKey);
}

public interface IPaymentService
{
    Task<ServiceResult<PaymentSummaryDto>> InitiateQrisAsync(
        string trackingToken,
        CancellationToken cancellationToken);

    Task<ServiceResult<OrderTrackingDto>> SimulateAsync(
        string trackingToken,
        CancellationToken cancellationToken);

    Task<WebhookResult> HandleWebhookAsync(
        MidtransNotificationRequest notification,
        string rawBody,
        CancellationToken cancellationToken);
}
