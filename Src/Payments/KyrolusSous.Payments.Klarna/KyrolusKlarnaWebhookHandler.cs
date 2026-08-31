using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Klarna;

public sealed class KyrolusKlarnaWebhookHandler(IOptions<KyrolusKlarnaOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Klarna";
    private readonly KyrolusKlarnaOptions _options = options.Value;

    // Klarna's order-management push notifications don't carry an HMAC signature; the documented
    // way to secure the receiving endpoint is HTTP Basic Auth using merchant-chosen credentials
    // (here, the same API credentials already configured for this provider).
    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiUsername) || string.IsNullOrWhiteSpace(_options.ApiPassword))
        {
            return Task.FromResult(false);
        }

        if (!TryGetHeader(headers, "Authorization", out var authHeader) ||
            !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader["Basic ".Length..].Trim()));
            var expected = $"{_options.ApiUsername}:{_options.ApiPassword}";
            return Task.FromResult(System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(decoded), Encoding.UTF8.GetBytes(expected)));
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
            var orderId = root.TryGetProperty("order_id", out var o) ? o.GetString() : null;
            var eventType = root.TryGetProperty("event_type", out var et) ? et.GetString() : "klarna.order.updated";

            // Klarna's push notification is a "something changed" ping, not an authoritative status;
            // callers should follow up with GetPaymentStatusAsync(orderId) rather than trust a guessed status here.
            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                EventType = eventType ?? "klarna.order.updated",
                ProviderName = "Klarna",
                TransactionId = orderId,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
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
}
