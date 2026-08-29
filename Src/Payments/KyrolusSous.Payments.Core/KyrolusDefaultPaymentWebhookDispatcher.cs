using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultPaymentWebhookDispatcher(HttpClient? httpClient = null) : IKyrolusPaymentWebhookDispatcher
{
    private readonly ConcurrentDictionary<string, KyrolusWebhookDispatchSubscription> _subscriptions = new();

    public void RegisterSubscription(KyrolusWebhookDispatchSubscription subscription)
    {
        _subscriptions[subscription.SubscriptionId] = subscription;
    }

    public async Task<IReadOnlyList<KyrolusWebhookDeliveryAttemptResult>> DispatchEventAsync(
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var matching = _subscriptions.Values.Where(s =>
            s.SubscribedEventTypes.Contains("*", StringComparer.OrdinalIgnoreCase) ||
            s.SubscribedEventTypes.Contains(eventType, StringComparer.OrdinalIgnoreCase)).ToList();

        var results = new List<KyrolusWebhookDeliveryAttemptResult>();

        foreach (var sub in matching)
        {
            var hmac = ComputeHmacSignature(payloadJson, sub.SecretKey);

            if (httpClient is not null)
            {
                try
                {
                    using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    content.Headers.Add("X-Kyrolus-Signature", hmac);
                    content.Headers.Add("X-Kyrolus-Event", eventType);

                    var resp = await httpClient.PostAsync(sub.DestinationUrl, content, cancellationToken).ConfigureAwait(false);
                    results.Add(new KyrolusWebhookDeliveryAttemptResult
                    {
                        SubscriptionId = sub.SubscriptionId,
                        DestinationUrl = sub.DestinationUrl,
                        Succeeded = resp.IsSuccessStatusCode,
                        HttpStatusCode = (int)resp.StatusCode
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new KyrolusWebhookDeliveryAttemptResult
                    {
                        SubscriptionId = sub.SubscriptionId,
                        DestinationUrl = sub.DestinationUrl,
                        Succeeded = false,
                        HttpStatusCode = 0,
                        ErrorMessage = ex.Message
                    });
                }
            }
            else
            {
                // In-memory simulated success
                results.Add(new KyrolusWebhookDeliveryAttemptResult
                {
                    SubscriptionId = sub.SubscriptionId,
                    DestinationUrl = sub.DestinationUrl,
                    Succeeded = true,
                    HttpStatusCode = 200
                });
            }
        }

        return results.AsReadOnly();
    }

    public static string ComputeHmacSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool VerifyHmacSignature(string payload, string secret, string providedHexSignature)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(providedHexSignature))
        {
            return false;
        }

        var expectedHex = ComputeHmacSignature(payload, secret);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex);
        var providedBytes = Encoding.UTF8.GetBytes(providedHexSignature.Trim().ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
