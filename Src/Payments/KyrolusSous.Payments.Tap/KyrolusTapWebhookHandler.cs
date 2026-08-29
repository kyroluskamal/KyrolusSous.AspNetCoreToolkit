using System.Text.Json;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Tap;

public sealed class KyrolusTapWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "Tap";

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
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : Guid.NewGuid().ToString("N");
            var statusStr = root.TryGetProperty("status", out var sProp) ? sProp.GetString() : null;

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = id ?? Guid.NewGuid().ToString("N"),
                EventType = statusStr ?? "charge.updated",
                ProviderName = "Tap",
                TransactionId = id,
                PaymentStatus = statusStr == "CAPTURED" ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Pending,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
    }
}
