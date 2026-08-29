namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentFactory
{
    IKyrolusPaymentProvider GetProvider(string providerName);
    TProvider GetProvider<TProvider>() where TProvider : IKyrolusPaymentProvider;
    IKyrolusPaymentProvider? GetProviderForCurrency(string currency);
    IReadOnlyList<IKyrolusPaymentProvider> GetAllProviders();
    IKyrolusWebhookHandler? GetWebhookHandler(string providerName);
}
