using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Klarna;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusKlarna(this IServiceCollection services, Action<KyrolusKlarnaOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusKlarnaPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusKlarnaPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusKlarnaWebhookHandler>();
        return services;
    }
}
