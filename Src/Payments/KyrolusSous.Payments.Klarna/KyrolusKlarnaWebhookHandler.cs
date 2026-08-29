using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Klarna;

public sealed class KyrolusKlarnaWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "Klarna";

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(payload));
    }

    public Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = "klarna.order_completed",
            ProviderName = "Klarna",
            RawPayload = payload
        });
    }
}
