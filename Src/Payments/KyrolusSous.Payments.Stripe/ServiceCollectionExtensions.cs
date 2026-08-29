using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Stripe;

public sealed class KyrolusDefaultStripeOptionsProvider(IOptions<KyrolusStripeOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusStripeOptions>
{
    public ValueTask<KyrolusStripeOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusStripe(this IServiceCollection services, Action<KyrolusStripeOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusStripePaymentProvider>();
        services.AddHttpClient<KyrolusStripeSubscriptionProvider>();
        services.AddHttpClient<KyrolusStripeCustomerVaultProvider>();

        services.AddSingleton<IKyrolusPaymentProvider, KyrolusStripePaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusStripeWebhookHandler>();
        services.AddSingleton<IKyrolusSubscriptionProvider, KyrolusStripeSubscriptionProvider>();
        services.AddSingleton<IKyrolusCustomerVaultProvider, KyrolusStripeCustomerVaultProvider>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusStripeOptions>, KyrolusDefaultStripeOptionsProvider>();
        return services;
    }
}
