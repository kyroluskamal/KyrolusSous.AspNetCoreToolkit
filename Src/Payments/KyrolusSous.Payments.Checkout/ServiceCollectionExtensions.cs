using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Checkout;

public sealed class KyrolusDefaultCheckoutOptionsProvider(IOptions<KyrolusCheckoutOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusCheckoutOptions>
{
    public ValueTask<KyrolusCheckoutOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusCheckout(this IServiceCollection services, Action<KyrolusCheckoutOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusCheckoutPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusCheckoutPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusCheckoutWebhookHandler>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusCheckoutOptions>, KyrolusDefaultCheckoutOptionsProvider>();
        return services;
    }
}
