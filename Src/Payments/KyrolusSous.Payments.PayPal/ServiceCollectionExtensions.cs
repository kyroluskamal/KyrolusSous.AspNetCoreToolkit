using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.PayPal;

public sealed class KyrolusDefaultPayPalOptionsProvider(IOptions<KyrolusPayPalOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusPayPalOptions>
{
    public ValueTask<KyrolusPayPalOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusPayPal(this IServiceCollection services, Action<KyrolusPayPalOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusPayPalPaymentProvider>();
        services.AddHttpClient<KyrolusPayPalSubscriptionProvider>();

        services.AddSingleton<IKyrolusPaymentProvider, KyrolusPayPalPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusPayPalWebhookHandler>();
        services.AddSingleton<IKyrolusSubscriptionProvider, KyrolusPayPalSubscriptionProvider>();
        services.AddSingleton<IKyrolusCustomerVaultProvider, KyrolusPayPalCustomerVaultProvider>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusPayPalOptions>, KyrolusDefaultPayPalOptionsProvider>();
        return services;
    }
}
