using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Fawry;

public sealed class KyrolusFawryWebhookHandler(IOptions<KyrolusFawryOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Fawry";
    private readonly KyrolusFawryOptions _options = options.Value;

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
            var refNumber = root.TryGetProperty("referenceNumber", out var r) ? r.GetString() : Guid.NewGuid().ToString("N");
            var statusStr = root.TryGetProperty("orderStatus", out var os) ? os.GetString() : "PAID";

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = refNumber ?? Guid.NewGuid().ToString("N"),
                EventType = statusStr ?? "UNKNOWN",
                ProviderName = "Fawry",
                TransactionId = refNumber,
                PaymentStatus = statusStr == "PAID" ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Pending,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
    }
}
