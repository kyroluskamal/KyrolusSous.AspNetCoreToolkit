using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.ApiKey;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures Kyrolus API Key authentication.
    /// </summary>
    public static AuthenticationBuilder AddKyrolusApiKeyAuth(
        this IServiceCollection services,
        Action<KyrolusApiKeyAuthenticationOptions>? configure = null)
    {
        services.TryAddSingleton<IKyrolusApiKeyGenerator, KyrolusApiKeyGenerator>();

        return services.AddAuthentication(KyrolusApiKeyAuthenticationOptions.DefaultScheme)
            .AddScheme<KyrolusApiKeyAuthenticationOptions, KyrolusApiKeyAuthenticationHandler>(
                KyrolusApiKeyAuthenticationOptions.DefaultScheme,
                configure ?? (_ => { }));
    }

    /// <summary>
    /// Registers a custom API key validator implementation.
    /// </summary>
    public static IServiceCollection AddKyrolusApiKeyValidator<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TValidator>(this IServiceCollection services)
        where TValidator : class, IKyrolusApiKeyValidator
    {
        services.AddScoped<IKyrolusApiKeyValidator, TValidator>();
        return services;
    }
}
