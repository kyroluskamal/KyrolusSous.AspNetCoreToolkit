using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Stripe;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusStripe(this IServiceCollection services, Action<KyrolusStripeOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusStripePaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusStripePaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusStripeWebhookHandler>();
        return services;
    }
}
