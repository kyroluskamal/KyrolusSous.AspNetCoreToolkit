using KyrolusSous.Payments.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Payments.Fawry;

public sealed class KyrolusDefaultFawryOptionsProvider(IOptions<KyrolusFawryOptions> options)
    : IKyrolusPaymentOptionsProvider<KyrolusFawryOptions>
{
    public ValueTask<KyrolusFawryOptions> GetOptionsAsync(string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(options.Value);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFawry(this IServiceCollection services, Action<KyrolusFawryOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<KyrolusFawryPaymentProvider>();
        services.AddSingleton<IKyrolusPaymentProvider, KyrolusFawryPaymentProvider>();
        services.AddSingleton<IKyrolusWebhookHandler, KyrolusFawryWebhookHandler>();
        services.AddSingleton<IKyrolusCustomerVaultProvider, KyrolusFawryCustomerVaultProvider>();
        services.AddSingleton<IKyrolusPaymentOptionsProvider<KyrolusFawryOptions>, KyrolusDefaultFawryOptionsProvider>();
        return services;
    }
}
