using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Mollie;

public sealed class KyrolusDefaultMollieOptionsProvider(IOptions<KyrolusMollieOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusMollieOptions>
{
    public ValueTask<KyrolusMollieOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusMollie(this IServiceCollection services, Action<KyrolusMollieOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusMolliePaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusMolliePaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusMollieWebhookHandler>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusMollieOptions>, KyrolusDefaultMollieOptionsProvider>();
        return services;
    }
}
