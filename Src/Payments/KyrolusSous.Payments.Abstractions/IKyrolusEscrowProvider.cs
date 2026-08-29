namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusEscrowProvider
{
    string ProviderName { get; }
    Task<KyrolusEscrowResult> HoldFundsAsync(KyrolusHoldFundsRequest request, CancellationToken cancellationToken = default);
    Task<KyrolusEscrowResult> CaptureHeldFundsAsync(string holdId, decimal? amount = null, CancellationToken cancellationToken = default);
    Task<bool> VoidHeldFundsAsync(string holdId, CancellationToken cancellationToken = default);
}
