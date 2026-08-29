using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Checkout;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCheckout(this IServiceCollection services, Action<KyrolusCheckoutOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusCheckoutPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusCheckoutPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusCheckoutWebhookHandler>();
        return services;
    }
}
