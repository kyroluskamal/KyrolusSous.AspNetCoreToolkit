using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Mollie;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusMollie(this IServiceCollection services, Action<KyrolusMollieOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusMolliePaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusMolliePaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusMollieWebhookHandler>();
        return services;
    }
}
