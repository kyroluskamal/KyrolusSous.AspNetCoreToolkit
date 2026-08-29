using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.PayPal;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusPayPal(this IServiceCollection services, Action<KyrolusPayPalOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusPayPalPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusPayPalPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusPayPalWebhookHandler>();
        return services;
    }
}
