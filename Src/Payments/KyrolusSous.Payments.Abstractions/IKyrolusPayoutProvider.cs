namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPayoutProvider
{
    string ProviderName { get; }
    Task<KyrolusPayoutResult> SendPayoutAsync(KyrolusPayoutRequest request, CancellationToken cancellationToken = default);
    Task<KyrolusBatchPayoutResult> SendBatchPayoutAsync(KyrolusBatchPayoutRequest request, CancellationToken cancellationToken = default);
    Task<KyrolusPayoutResult> GetPayoutStatusAsync(string payoutId, CancellationToken cancellationToken = default);
    Task<bool> CancelPayoutAsync(string payoutId, CancellationToken cancellationToken = default);
}
