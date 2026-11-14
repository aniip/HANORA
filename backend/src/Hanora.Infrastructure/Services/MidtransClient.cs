using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hanora.Application.Payments;
using Hanora.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace Hanora.Infrastructure.Services;

/// <summary>
/// Klien Midtrans Snap. Server key hanya dipakai di backend dan tidak pernah
/// dikirim ke frontend.
/// </summary>
public sealed class MidtransClient(
    HttpClient httpClient,
    MidtransOptions options,
    ILogger<MidtransClient> logger) : IMidtransClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<SnapTransactionResult> CreateSnapTransactionAsync(
        SnapTransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ServerKey))
        {
            throw new MidtransException(
                "MIDTRANS_SERVER_KEY belum dikonfigurasi untuk mode ini.");
        }

        var payload = new
        {
            transaction_details = new
            {
                order_id = request.OrderId,
                gross_amount = request.GrossAmount
            },
            customer_details = new
            {
                first_name = request.CustomerName
            },
            callbacks = new
            {
                finish = request.FinishUrl
            },
            enabled_payments = new[] { "other_qris" }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.SnapUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{options.ServerKey}:")));
        httpRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Midtrans Snap menolak transaksi {OrderId} dengan status {Status}: {Body}",
                request.OrderId,
                (int)response.StatusCode,
                body);

            throw new MidtransException(
                $"Midtrans menolak pembuatan transaksi ({(int)response.StatusCode}).");
        }

        var parsed = JsonSerializer.Deserialize<SnapResponse>(body, JsonOptions);

        if (parsed is null ||
            string.IsNullOrWhiteSpace(parsed.Token) ||
            string.IsNullOrWhiteSpace(parsed.RedirectUrl))
        {
            logger.LogWarning(
                "Respons Midtrans Snap tidak lengkap untuk {OrderId}: {Body}",
                request.OrderId,
                body);

            throw new MidtransException("Respons Midtrans tidak lengkap.");
        }

        return new SnapTransactionResult(parsed.Token, parsed.RedirectUrl);
    }

    public bool VerifySignature(
        string orderId,
        string statusCode,
        string grossAmount,
        string signatureKey)
    {
        if (string.IsNullOrWhiteSpace(options.ServerKey) ||
            string.IsNullOrWhiteSpace(signatureKey))
        {
            return false;
        }

        var raw = $"{orderId}{statusCode}{grossAmount}{options.ServerKey}";
        var expected = Convert.ToHexString(
            SHA512.HashData(Encoding.UTF8.GetBytes(raw)));

        var provided = signatureKey.Trim();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(provided.ToUpperInvariant()));
    }

    private sealed record SnapResponse(string Token, string RedirectUrl);
}

public sealed class MidtransException(string message) : Exception(message);
