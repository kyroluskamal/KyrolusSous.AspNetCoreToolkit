using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Tap;

public sealed class KyrolusDefaultTapOptionsProvider(IOptions<KyrolusTapOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusTapOptions>
{
    public ValueTask<KyrolusTapOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusTap(this IServiceCollection services, Action<KyrolusTapOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusTapPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusTapPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusTapWebhookHandler>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusTapOptions>, KyrolusDefaultTapOptionsProvider>();
        return services;
    }
}
