using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Klarna;

public sealed class KyrolusDefaultKlarnaOptionsProvider(IOptions<KyrolusKlarnaOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusKlarnaOptions>
{
    public ValueTask<KyrolusKlarnaOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusKlarna(this IServiceCollection services, Action<KyrolusKlarnaOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusKlarnaPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusKlarnaPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusKlarnaWebhookHandler>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusKlarnaOptions>, KyrolusDefaultKlarnaOptionsProvider>();
        return services;
    }
}
