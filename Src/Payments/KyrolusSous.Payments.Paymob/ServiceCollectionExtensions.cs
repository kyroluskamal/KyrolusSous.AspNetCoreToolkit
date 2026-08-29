using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Paymob;

public sealed class KyrolusDefaultPaymobOptionsProvider(IOptions<KyrolusPaymobOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusPaymobOptions>
{
    public ValueTask<KyrolusPaymobOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusPaymob(this IServiceCollection services, Action<KyrolusPaymobOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusPaymobPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusPaymobPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusPaymobWebhookHandler>();
        services.AddSingleton<IKyrolusCustomerVaultProvider, KyrolusPaymobCustomerVaultProvider>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusPaymobOptions>, KyrolusDefaultPaymobOptionsProvider>();
        return services;
    }
}
