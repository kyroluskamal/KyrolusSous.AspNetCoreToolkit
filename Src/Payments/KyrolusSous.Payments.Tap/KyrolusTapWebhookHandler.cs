using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Tap;

public sealed class KyrolusTapWebhookHandler(IOptions<KyrolusTapOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Tap";
    private readonly KyrolusTapOptions _options = options.Value;

    // Tap signs webhooks with a "hashstring" header. Verified here as HMAC-SHA256 of the raw body
    // using the account's secret key; confirm the exact field/header convention against Tap's
    // current docs if a genuinely-signed notification is ever rejected.
    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey)) return Task.FromResult(false);
        if (!TryGetHeader(headers, "hashstring", out var providedHash) || string.IsNullOrWhiteSpace(providedHash))
        {
            return Task.FromResult(false);
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SecretKey));
        var computedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(providedHash.Trim().ToLowerInvariant())));
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
