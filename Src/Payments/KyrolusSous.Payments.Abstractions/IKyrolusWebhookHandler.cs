namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusWebhookHandler
{
    string ProviderName { get; }
    Task<bool> ValidateSignatureAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default);
    Task<KyrolusWebhookEvent?> ParseEventAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default);
}
