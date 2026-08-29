using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Fawry;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFawry(this IServiceCollection services, Action<KyrolusFawryOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusFawryPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusFawryPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusFawryWebhookHandler>();
        return services;
    }
}
