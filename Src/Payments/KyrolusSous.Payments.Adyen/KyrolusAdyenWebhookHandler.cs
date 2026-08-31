using System.Security.Cryptography;
using System.Text;
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
        if (string.IsNullOrWhiteSpace(_options.HmacKey)) return Task.FromResult(false);

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var items = doc.RootElement.GetProperty("notificationItems");
            var first = items.EnumerateArray().FirstOrDefault();
            var item = first.GetProperty("NotificationRequestItem");

            if (!item.TryGetProperty("additionalData", out var additionalData) ||
                !additionalData.TryGetProperty("hmacSignature", out var sigProp))
            {
                return Task.FromResult(false);
            }

            var providedSignature = sigProp.GetString() ?? string.Empty;
            var amount = item.GetProperty("amount");

            // Adyen's documented HMAC signing string: colon-joined fields, HMAC-SHA256 over the
            // hex-decoded key, result compared as Base64.
            var signingString = string.Join(':',
                GetString(item, "pspReference"),
                GetString(item, "originalReference"),
                GetString(item, "merchantAccountCode"),
                GetString(item, "merchantReference"),
                GetString(amount, "value"),
                GetString(amount, "currency"),
                GetString(item, "eventCode"),
                GetString(item, "success"));

            var keyBytes = Convert.FromHexString(_options.HmacKey);
            using var hmac = new HMACSHA256(keyBytes);
            var computedSignature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingString)));

            return Task.FromResult(CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(providedSignature)));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop)
            ? (prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? string.Empty : prop.GetRawText())
            : string.Empty;

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
