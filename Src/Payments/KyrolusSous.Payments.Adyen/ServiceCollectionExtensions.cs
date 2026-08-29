using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Adyen;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusAdyen(this IServiceCollection services, Action<KyrolusAdyenOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusAdyenPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusAdyenPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusAdyenWebhookHandler>();
        return services;
    }
}
