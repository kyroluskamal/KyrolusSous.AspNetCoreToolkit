using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Fawry;

public sealed class KyrolusFawryWebhookHandler(IOptions<KyrolusFawryOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "Fawry";
    private readonly KyrolusFawryOptions _options = options.Value;

    // Same "SHA256(data + SecurityKey)" scheme KyrolusFawryPaymentProvider.ComputeSignature already
    // uses for outbound requests. Field order per Fawry's V2 notification spec; verify against
    // current Fawry docs if notifications stop matching.
    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecurityKey)) return Task.FromResult(false);

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var providedSignature = root.TryGetProperty("signature", out var sigProp) ? sigProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(providedSignature)) return Task.FromResult(false);

            string Field(string name) => root.TryGetProperty(name, out var p)
                ? (p.ValueKind == JsonValueKind.String ? p.GetString() ?? string.Empty : p.GetRawText())
                : string.Empty;

            var rawData = Field("referenceNumber") + Field("merchantRefNumber") + Field("paymentAmount") +
                          Field("orderAmount") + Field("orderStatus") + Field("paymentMethod");

            using var sha256 = SHA256.Create();
            var computedHex = Convert.ToHexStringLower(sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData + _options.SecurityKey)));

            return Task.FromResult(CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant())));
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
