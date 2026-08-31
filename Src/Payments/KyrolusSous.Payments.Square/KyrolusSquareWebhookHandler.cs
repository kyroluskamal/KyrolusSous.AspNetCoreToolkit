using System.Text.Json;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Square;

public sealed class KyrolusSquareWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "Square";

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(payload));
    }

    public Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventId = root.TryGetProperty("event_id", out var e) ? e.GetString() : null;
            var eventType = root.TryGetProperty("type", out var t) ? t.GetString() : "payment.updated";

            string? transactionId = null;
            string? statusStr = null;
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("object", out var obj) &&
                obj.TryGetProperty("payment", out var payment))
            {
                transactionId = payment.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                statusStr = payment.TryGetProperty("status", out var sProp) ? sProp.GetString() : null;
            }

            var status = statusStr switch
            {
                "COMPLETED" => KyrolusPaymentStatus.Succeeded,
                "CANCELED" => KyrolusPaymentStatus.Cancelled,
                "FAILED" => KyrolusPaymentStatus.Failed,
                _ => KyrolusPaymentStatus.Pending
            };

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = eventId ?? Guid.NewGuid().ToString("N"),
                EventType = eventType ?? "payment.updated",
                ProviderName = "Square",
                TransactionId = transactionId,
                PaymentStatus = status,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
    }
}
