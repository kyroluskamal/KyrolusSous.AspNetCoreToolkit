using System.Text.Json;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusPayPalWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "PayPal";

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(headers.ContainsKey("PAYPAL-AUTH-ALGO") || headers.ContainsKey("paypal-auth-algo") || !string.IsNullOrWhiteSpace(payload));
    }

    public Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventId = root.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
            var eventType = root.GetProperty("event_type").GetString() ?? "unknown";

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = eventId,
                EventType = eventType,
                ProviderName = "PayPal",
                PaymentStatus = eventType.Contains("COMPLETED", StringComparison.OrdinalIgnoreCase) ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Pending,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
    }
}
