using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusPaymentFactory(
    IEnumerable<IKyrolusPaymentProvider> providers,
    IEnumerable<IKyrolusWebhookHandler> webhookHandlers,
    IEnumerable<IKyrolusSubscriptionProvider>? subscriptionProviders = null,
    IEnumerable<IKyrolusCustomerVaultProvider>? vaultProviders = null,
    IEnumerable<IKyrolusPaymentLinkProvider>? linkProviders = null,
    IEnumerable<IKyrolusMarketplaceProvider>? marketplaceProviders = null,
    IEnumerable<IKyrolusDisputeProvider>? disputeProviders = null,
    IEnumerable<IKyrolusPayoutProvider>? payoutProviders = null,
    IEnumerable<IKyrolusEscrowProvider>? escrowProviders = null,
    IEnumerable<IKyrolusVirtualCardProvider>? virtualCardProviders = null,
    IEnumerable<IKyrolusCryptoPaymentProvider>? cryptoProviders = null) : IKyrolusPaymentFactory
{
    private readonly Dictionary<string, IKyrolusPaymentProvider> _providers =
        providers.ToDictionary(p => p.ProviderName, p => p, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusWebhookHandler> _webhookHandlers =
        webhookHandlers.ToDictionary(h => h.ProviderName, h => h, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusSubscriptionProvider> _subscriptionProviders =
        (subscriptionProviders ?? []).ToDictionary(s => s.ProviderName, s => s, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusCustomerVaultProvider> _vaultProviders =
        (vaultProviders ?? []).ToDictionary(v => v.ProviderName, v => v, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusPaymentLinkProvider> _linkProviders =
        (linkProviders ?? []).ToDictionary(l => l.ProviderName, l => l, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusMarketplaceProvider> _marketplaceProviders =
        (marketplaceProviders ?? []).ToDictionary(m => m.ProviderName, m => m, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusDisputeProvider> _disputeProviders =
        (disputeProviders ?? []).ToDictionary(d => d.ProviderName, d => d, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusPayoutProvider> _payoutProviders =
        (payoutProviders ?? []).ToDictionary(p => p.ProviderName, p => p, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusEscrowProvider> _escrowProviders =
        (escrowProviders ?? []).ToDictionary(e => e.ProviderName, e => e, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusVirtualCardProvider> _virtualCardProviders =
        (virtualCardProviders ?? []).ToDictionary(v => v.ProviderName, v => v, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IKyrolusCryptoPaymentProvider> _cryptoProviders =
        (cryptoProviders ?? []).ToDictionary(c => c.ProviderName, c => c, StringComparer.OrdinalIgnoreCase);

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

    public IKyrolusSubscriptionProvider? GetSubscriptionProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _subscriptionProviders.TryGetValue(providerName, out var sub) ? sub : null;
    }

    public IKyrolusCustomerVaultProvider? GetVaultProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _vaultProviders.TryGetValue(providerName, out var vault) ? vault : null;
    }

    public IKyrolusPaymentLinkProvider? GetPaymentLinkProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _linkProviders.TryGetValue(providerName, out var link) ? link : null;
    }

    public IKyrolusMarketplaceProvider? GetMarketplaceProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _marketplaceProviders.TryGetValue(providerName, out var market) ? market : null;
    }

    public IKyrolusDisputeProvider? GetDisputeProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _disputeProviders.TryGetValue(providerName, out var disp) ? disp : null;
    }

    public IKyrolusPayoutProvider? GetPayoutProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _payoutProviders.TryGetValue(providerName, out var payout) ? payout : null;
    }

    public IKyrolusEscrowProvider? GetEscrowProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _escrowProviders.TryGetValue(providerName, out var escrow) ? escrow : null;
    }

    public IKyrolusVirtualCardProvider? GetVirtualCardProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _virtualCardProviders.TryGetValue(providerName, out var vc) ? vc : null;
    }

    public IKyrolusCryptoPaymentProvider? GetCryptoProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        return _cryptoProviders.TryGetValue(providerName, out var crypto) ? crypto : null;
    }
}
