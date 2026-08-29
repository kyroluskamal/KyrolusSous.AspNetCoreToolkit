using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Mollie;

public sealed class KyrolusMollieWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "Mollie";

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(payload));
    }

    public Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        var id = payload.StartsWith("id=") ? payload[3..] : payload;
        return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
        {
            EventId = id,
            EventType = "payment.updated",
            ProviderName = "Mollie",
            TransactionId = id,
            RawPayload = payload
        });
    }
}
