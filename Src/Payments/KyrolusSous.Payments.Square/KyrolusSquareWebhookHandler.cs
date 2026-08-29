using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Square;

public sealed class KyrolusSquareWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "Square";

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(payload));
    }

    public Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = "payment.updated",
            ProviderName = "Square",
            RawPayload = payload
        });
    }
}
