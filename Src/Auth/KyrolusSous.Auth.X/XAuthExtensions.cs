using AspNet.Security.OAuth.Twitter;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.X;

/// <summary>
/// Extension methods for configuring Kyrolus X (formerly Twitter) Authentication.
/// </summary>
public static class XAuthExtensions
{
    /// <summary>
    /// Adds X (formerly Twitter) OAuth 2.0 authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus X options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="TwitterAuthenticationOptions"/>.
    /// </param>
    public static AuthenticationBuilder AddKyrolusXAuth(
        this IServiceCollection services,
        Action<KyrolusXAuthOptions> configure,
        Action<TwitterAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusXAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds X (formerly Twitter) OAuth 2.0 authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus X options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusXAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusXAuthOptions> configure,
        Action<TwitterAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusXAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.X;
        var isConfigured = !string.IsNullOrWhiteSpace(options.ClientId)
                           && !string.IsNullOrWhiteSpace(options.ClientSecret);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(options, provider, isConfigured, "ClientId, ClientSecret");

        var scheme = options.ResolveScheme(provider);

        builder.AddTwitter(scheme, options.ResolveDisplayName(provider), twitter =>
        {
            twitter.ClientId = options.ClientId;
            twitter.ClientSecret = options.ClientSecret;

            // X rejects an authorization request without PKCE. Setting it here rather than
            // trusting the default means an escape-hatch caller has to disable it deliberately.
            twitter.UsePkce = true;

            // X only issues refresh tokens when offline.access is among the granted scopes.
            if (options.RequestRefreshToken && !options.Scopes.Contains("offline.access"))
            {
                options.Scopes.Add("offline.access");
            }

            foreach (var field in options.UserFields)
            {
                if (!string.IsNullOrWhiteSpace(field) && !twitter.UserFields.Contains(field))
                {
                    twitter.UserFields.Add(field);
                }
            }

            KyrolusExternalAuthConfigurator.Apply(twitter, options, provider);

            configureProvider?.Invoke(twitter);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
