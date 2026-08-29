using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Tap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusTap(this IServiceCollection services, Action<KyrolusTapOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusTapPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusTapPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusTapWebhookHandler>();
        return services;
    }
}
