namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentFactory
{
    IKyrolusPaymentProvider GetProvider(string providerName);
    TProvider GetProvider<TProvider>() where TProvider : IKyrolusPaymentProvider;
    IKyrolusPaymentProvider? GetProviderForCurrency(string currency);
    IReadOnlyList<IKyrolusPaymentProvider> GetAllProviders();
    IKyrolusWebhookHandler? GetWebhookHandler(string providerName);
    IKyrolusSubscriptionProvider? GetSubscriptionProvider(string providerName);
    IKyrolusCustomerVaultProvider? GetVaultProvider(string providerName);
    IKyrolusPaymentLinkProvider? GetPaymentLinkProvider(string providerName);
    IKyrolusMarketplaceProvider? GetMarketplaceProvider(string providerName);
    IKyrolusDisputeProvider? GetDisputeProvider(string providerName);
    IKyrolusPayoutProvider? GetPayoutProvider(string providerName);
    IKyrolusEscrowProvider? GetEscrowProvider(string providerName);
    IKyrolusVirtualCardProvider? GetVirtualCardProvider(string providerName);
    IKyrolusCryptoPaymentProvider? GetCryptoProvider(string providerName);
}
