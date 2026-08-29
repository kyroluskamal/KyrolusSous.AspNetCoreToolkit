using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Adyen;

public sealed class KyrolusAdyenWebhookHandler(IOptions<KyrolusAdyenOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Adyen";
    private readonly KyrolusAdyenOptions _options = options.Value;

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
            var items = root.GetProperty("notificationItems");
            var first = items.EnumerateArray().FirstOrDefault();
            var item = first.GetProperty("NotificationRequestItem");

            var eventCode = item.GetProperty("eventCode").GetString() ?? "AUTHORISATION";
            var success = item.GetProperty("success").GetString() == "true";
            var pspRef = item.GetProperty("pspReference").GetString() ?? Guid.NewGuid().ToString("N");

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = pspRef,
                EventType = eventCode,
                ProviderName = "Adyen",
                TransactionId = pspRef,
                PaymentStatus = success ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Failed,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
    }
}
