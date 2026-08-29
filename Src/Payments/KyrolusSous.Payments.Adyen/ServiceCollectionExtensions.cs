using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Adyen;

public sealed class KyrolusDefaultAdyenOptionsProvider(IOptions<KyrolusAdyenOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusAdyenOptions>
{
    public ValueTask<KyrolusAdyenOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusAdyen(this IServiceCollection services, Action<KyrolusAdyenOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusAdyenPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusAdyenPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusAdyenWebhookHandler>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusAdyenOptions>, KyrolusDefaultAdyenOptionsProvider>();
        return services;
    }
}
