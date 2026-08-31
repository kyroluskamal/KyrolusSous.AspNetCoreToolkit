using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Paymob;

public sealed class KyrolusPaymobWebhookHandler(IOptions<KyrolusPaymobOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Paymob";
    private readonly KyrolusPaymobOptions _options = options.Value;

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.HmacSecret)) return Task.FromResult(false);
        if (!TryGetHeader(headers, "hmac", out var providedHmac) || string.IsNullOrWhiteSpace(providedHmac))
        {
            return Task.FromResult(false);
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var obj = root.TryGetProperty("obj", out var o) ? o : root;

            // Paymob's documented HMAC field order (concatenated, no separators).
            var sourceData = obj.TryGetProperty("source_data", out var sd) ? sd : default;
            var order = obj.TryGetProperty("order", out var ord) ? ord : default;
            var concatenated = string.Concat(
                GetHmacField(obj, "amount_cents"),
                GetHmacField(obj, "created_at"),
                GetHmacField(obj, "currency"),
                GetHmacField(obj, "error_occured"),
                GetHmacField(obj, "has_parent_transaction"),
                GetHmacField(obj, "id"),
                GetHmacField(obj, "integration_id"),
                GetHmacField(obj, "is_3d_secure"),
                GetHmacField(obj, "is_auction"),
                GetHmacField(obj, "is_capture"),
                GetHmacField(obj, "is_refunded"),
                GetHmacField(obj, "is_standalone_payment"),
                GetHmacField(obj, "is_voided"),
                GetHmacField(order, "id"),
                GetHmacField(obj, "owner"),
                GetHmacField(obj, "pending"),
                GetHmacField(sourceData, "pan"),
                GetHmacField(sourceData, "sub_type"),
                GetHmacField(sourceData, "type"),
                GetHmacField(obj, "success"));

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.HmacSecret));
            var computedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated)));

            return Task.FromResult(CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(providedHmac.Trim().ToLowerInvariant())));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static string GetHmacField(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var prop))
        {
            return string.Empty;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            JsonValueKind.String => prop.GetString() ?? string.Empty,
            _ => prop.GetRawText()
        };
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
            var obj = root.TryGetProperty("obj", out var o) ? o : root;

            var id = obj.TryGetProperty("id", out var idProp) ? idProp.GetInt64().ToString() : Guid.NewGuid().ToString("N");
            var success = obj.TryGetProperty("success", out var sProp) && sProp.GetBoolean();

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = id,
                EventType = success ? "TRANSACTION_SUCCESS" : "TRANSACTION_FAILED",
                ProviderName = "Paymob",
                TransactionId = id,
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
