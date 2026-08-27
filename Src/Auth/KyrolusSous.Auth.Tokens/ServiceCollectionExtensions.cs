using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.Tokens;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Kyrolus user token services for email confirmation, password resets, and verification flows.
    /// </summary>
    public static IServiceCollection AddKyrolusUserTokens(
        this IServiceCollection services,
        Action<KyrolusUserTokenOptions>? configure = null)
    {
        var options = new KyrolusUserTokenOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IKyrolusUserTokenService, KyrolusUserTokenService>();

        return services;
    }
}
