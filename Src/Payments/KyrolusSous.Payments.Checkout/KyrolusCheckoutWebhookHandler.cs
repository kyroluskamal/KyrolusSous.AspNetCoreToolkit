using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Checkout;

public sealed class KyrolusCheckoutWebhookHandler(IOptions<KyrolusCheckoutOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Checkout";
    private readonly KyrolusCheckoutOptions _options = options.Value;

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret)) return Task.FromResult(false);
        if (!TryGetHeader(headers, "cko-signature", out var providedSignature) || string.IsNullOrWhiteSpace(providedSignature))
        {
            return Task.FromResult(false);
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var computedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant())));
    }

    private static bool TryGetHeader(IDictionary<string, string> headers, string name, out string value)
    {
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }
        value = string.Empty;
        return false;
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
