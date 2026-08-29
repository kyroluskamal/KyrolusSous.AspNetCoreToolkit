using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusMockWebhookHandler : IKyrolusWebhookHandler
{
    public string ProviderName => "Mock";

    public Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(headers.ContainsKey("X-Mock-Signature") || !string.IsNullOrWhiteSpace(payload));
    }

    public Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<KyrolusWebhookEvent?>(new KyrolusWebhookEvent
        {
            EventId = $"mock_evt_{Guid.NewGuid():N}",
            EventType = "payment.succeeded",
            ProviderName = "Mock",
            TransactionId = $"mock_tx_{Guid.NewGuid():N}",
            PaymentStatus = KyrolusPaymentStatus.Succeeded,
            RawPayload = payload
        });
    }
}
