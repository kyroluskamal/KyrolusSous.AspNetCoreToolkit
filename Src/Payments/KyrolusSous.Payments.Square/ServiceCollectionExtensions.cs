using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Square;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusSquare(this IServiceCollection services, Action<KyrolusSquareOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusSquarePaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusSquarePaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusSquareWebhookHandler>();
        return services;
    }
}
