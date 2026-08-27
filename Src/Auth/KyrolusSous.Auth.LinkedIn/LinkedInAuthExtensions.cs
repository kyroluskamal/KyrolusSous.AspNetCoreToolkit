using AspNet.Security.OAuth.LinkedIn;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.LinkedIn;

/// <summary>
/// Extension methods for configuring Kyrolus LinkedIn Authentication.
/// </summary>
public static class LinkedInAuthExtensions
{
    /// <summary>
    /// Adds LinkedIn authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus LinkedIn options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="LinkedInAuthenticationOptions"/>.
    /// </param>
    public static AuthenticationBuilder AddKyrolusLinkedInAuth(
        this IServiceCollection services,
        Action<KyrolusLinkedInAuthOptions> configure,
        Action<LinkedInAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusLinkedInAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds LinkedIn authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus LinkedIn options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusLinkedInAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusLinkedInAuthOptions> configure,
        Action<LinkedInAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusLinkedInAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.LinkedIn;
        var isConfigured = !string.IsNullOrWhiteSpace(options.ClientId)
                           && !string.IsNullOrWhiteSpace(options.ClientSecret);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(options, provider, isConfigured, "ClientId, ClientSecret");

        var scheme = options.ResolveScheme(provider);

        builder.AddLinkedIn(scheme, options.ResolveDisplayName(provider), linkedIn =>
        {
            linkedIn.ClientId = options.ClientId;
            linkedIn.ClientSecret = options.ClientSecret;

            KyrolusExternalAuthConfigurator.Apply(linkedIn, options, provider);

            configureProvider?.Invoke(linkedIn);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
