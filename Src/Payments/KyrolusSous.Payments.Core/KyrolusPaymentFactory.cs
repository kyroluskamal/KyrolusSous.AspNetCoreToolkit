using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusPaymentFactory(
    IEnumerable<IKyrolusPaymentProvider> providers,
    IEnumerable<IKyrolusWebhookHandler> webhookHandlers) : IKyrolusPaymentFactory
{
    private readonly Dictionary<string, IKyrolusPaymentProvider> _providers =
        providers.ToDictionary(p => p.ProviderName, p => p, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusWebhookHandler> _webhookHandlers =
        webhookHandlers.ToDictionary(h => h.ProviderName, h => h, StringComparer.OrdinalIgnoreCase);

    public IKyrolusPaymentProvider GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be empty.", nameof(providerName));
        }

        if (_providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"Payment provider '{providerName}' is not registered.");
    }

    public TProvider GetProvider<TProvider>() where TProvider : IKyrolusPaymentProvider
    {
        var match = _providers.Values.OfType<TProvider>().FirstOrDefault();
        if (match is not null)
        {
            return match;
        }

        throw new KeyNotFoundException($"Payment provider of type '{typeof(TProvider).Name}' is not registered.");
    }

    public IKyrolusPaymentProvider? GetProviderForCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return null;

        return _providers.Values.FirstOrDefault(p =>
            p.SupportedCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase) ||
            p.SupportedCurrencies.Contains("*"));
    }

    public IReadOnlyList<IKyrolusPaymentProvider> GetAllProviders() => _providers.Values.ToList().AsReadOnly();

    public IKyrolusWebhookHandler? GetWebhookHandler(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _webhookHandlers.TryGetValue(providerName, out var handler) ? handler : null;
    }
}
