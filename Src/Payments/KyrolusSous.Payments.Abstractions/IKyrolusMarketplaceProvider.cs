namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusMarketplaceProvider
{
    string ProviderName { get; }
    Task<KyrolusMerchantAccountResult> CreateConnectedAccountAsync(KyrolusMerchantAccountRequest request, CancellationToken cancellationToken = default);
    Task<KyrolusSplitTransferResult> TransferToConnectedAccountAsync(KyrolusSplitTransferRequest request, CancellationToken cancellationToken = default);
}
