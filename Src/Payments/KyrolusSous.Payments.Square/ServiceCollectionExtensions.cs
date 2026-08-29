using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Square;

public sealed class KyrolusDefaultSquareOptionsProvider(IOptions<KyrolusSquareOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusSquareOptions>
{
    public ValueTask<KyrolusSquareOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusSquare(this IServiceCollection services, Action<KyrolusSquareOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusSquarePaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusSquarePaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusSquareWebhookHandler>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusSquareOptions>, KyrolusDefaultSquareOptionsProvider>();
        return services;
    }
}
