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
        if (string.IsNullOrWhiteSpace(_options.HmacSecret)) return Task.FromResult(true);
        return Task.FromResult(headers.ContainsKey("hmac") || headers.ContainsKey("HMAC") || !string.IsNullOrWhiteSpace(payload));
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
