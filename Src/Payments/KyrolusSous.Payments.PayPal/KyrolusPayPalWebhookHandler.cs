using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusPayPalWebhookHandler(
    HttpClient httpClient,
    IOptions<KyrolusPayPalOptions> options) : IKyrolusWebhookHandler
{
    public string ProviderName => "PayPal";
    private readonly KyrolusPayPalOptions _options = options.Value;

    public async Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookId)) return false;

        if (!TryGetHeader(headers, "PAYPAL-AUTH-ALGO", out var authAlgo) ||
            !TryGetHeader(headers, "PAYPAL-CERT-URL", out var certUrl) ||
            !TryGetHeader(headers, "PAYPAL-TRANSMISSION-ID", out var transmissionId) ||
            !TryGetHeader(headers, "PAYPAL-TRANSMISSION-SIG", out var transmissionSig) ||
            !TryGetHeader(headers, "PAYPAL-TRANSMISSION-TIME", out var transmissionTime))
        {
            return false;
        }

        try
        {
            var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            using var webhookEventDoc = JsonDocument.Parse(payload);

            var verifyPayload = new
            {
                auth_algo = authAlgo,
                cert_url = certUrl,
                transmission_id = transmissionId,
                transmission_sig = transmissionSig,
                transmission_time = transmissionTime,
                webhook_id = _options.WebhookId,
                webhook_event = webhookEventDoc.RootElement
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/notifications/verify-webhook-signature")
            {
                Content = new StringContent(JsonSerializer.Serialize(verifyPayload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var resultDoc = JsonDocument.Parse(content);
            var status = resultDoc.RootElement.TryGetProperty("verification_status", out var s) ? s.GetString() : null;
            return string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var authBytes = Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")])
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private static bool TryGetHeader(IDictionary<string, string> headers, string name, out string value)
    {
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kvp.Value))
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
            var eventId = root.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
            var eventType = root.GetProperty("event_type").GetString() ?? "unknown";

            return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
            {
                EventId = eventId,
                EventType = eventType,
                ProviderName = "PayPal",
                PaymentStatus = eventType.Contains("COMPLETED", StringComparison.OrdinalIgnoreCase) ? KyrolusPaymentStatus.Succeeded : KyrolusPaymentStatus.Pending,
                RawPayload = payload
            });
        }
        catch
        {
            return Task.FromResult<KyrolusWebhookEvent?>(null);
        }
    }
}
