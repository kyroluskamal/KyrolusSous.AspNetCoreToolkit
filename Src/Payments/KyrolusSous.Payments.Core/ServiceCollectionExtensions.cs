using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Payments.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusPayments(this IServiceCollection services, bool registerMockProvider = true)
    {
        if (registerMockProvider)
        {
            services.AddSingleton<IKyrolusPaymentProvider, KyrolusMockPaymentProvider>();
            services.AddSingleton<IKyrolusWebhookHandler, KyrolusMockWebhookHandler>();
        }

        services.AddSingleton<IKyrolusPaymentFactory, KyrolusPaymentFactory>();
        return services;
    }
}
