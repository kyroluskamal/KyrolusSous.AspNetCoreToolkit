using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Stripe;

public sealed class KyrolusStripeWebhookHandler(IOptions<KyrolusStripeOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Stripe";
    private readonly KyrolusStripeOptions _options = options.Value;

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return Task.FromResult(true);
        }

        if (!headers.TryGetValue("Stripe-Signature", out var signatureHeader) &&
            !headers.TryGetValue("stripe-signature", out signatureHeader))
        {
            return Task.FromResult(false);
        }

        try
        {
            var items = signatureHeader.Split(',');
            string? timestamp = null;
            string? sig = null;
            foreach (var item in items)
            {
                var parts = item.Trim().Split('=', 2);
                if (parts.Length == 2)
                {
                    if (parts[0] == "t") timestamp = parts[1];
                    if (parts[0] == "v1") sig = parts[1];
                }
            }

            if (timestamp == null || sig == null) return Task.FromResult(false);

            var signedPayload = $"{timestamp}.{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
            var computedHash = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload)));

            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHash),
                Encoding.UTF8.GetBytes(sig));

            return Task.FromResult(isValid);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventId = root.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
            var eventType = root.GetProperty("type").GetString() ?? "unknown";

            string? txId = null;
            KyrolusPaymentStatus? status = null;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("object", out var obj))
            {
                if (obj.TryGetProperty("id", out var idProp)) txId = idProp.GetString();
                if (eventType.Contains("succeeded", StringComparison.OrdinalIgnoreCase)) status = KyrolusPaymentStatus.Succeeded;
                else if (eventType.Contains("failed", StringComparison.OrdinalIgnoreCase)) status = KyrolusPaymentStatus.Failed;
            }

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = eventId,
                EventType = eventType,
                ProviderName = "Stripe",
                TransactionId = txId,
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
