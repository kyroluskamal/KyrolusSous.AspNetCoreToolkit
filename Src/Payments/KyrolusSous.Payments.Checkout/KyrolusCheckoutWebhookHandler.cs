using System.Text.Json;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Checkout;

public sealed class KyrolusCheckoutWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "Checkout";

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
            var id = root.TryGetProperty("id", out var i) ? i.GetString() : Guid.NewGuid().ToString("N");
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : "payment_approved";

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = id ?? Guid.NewGuid().ToString("N"),
                EventType = type ?? "payment_approved",
                ProviderName = "Checkout",
                PaymentStatus = type?.Contains("approved") == true || type?.Contains("captured") == true ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Pending,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
    }
}
