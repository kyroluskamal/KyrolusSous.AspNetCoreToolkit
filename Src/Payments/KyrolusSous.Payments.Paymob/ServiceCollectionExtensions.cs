using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Paymob;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusPaymob(this IServiceCollection services, Action<KyrolusPaymobOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusPaymobPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusPaymobPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusPaymobWebhookHandler>();
        return services;
    }
}
