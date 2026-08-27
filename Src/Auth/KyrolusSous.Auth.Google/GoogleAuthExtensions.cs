using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Google;

/// <summary>
/// Extension methods for configuring Kyrolus Google Authentication.
/// </summary>
public static class GoogleAuthExtensions
{
    /// <summary>
    /// Adds Google OAuth 2.0 / OpenID Connect authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus Google options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="GoogleOptions"/> for anything the Kyrolus options do not surface.
    /// </param>
    /// <example>
    /// <code>
    /// services.AddKyrolusGoogleAuth(options =>
    /// {
    ///     options.ClientId = configuration["Auth:Google:ClientId"]!;
    ///     options.ClientSecret = configuration["Auth:Google:ClientSecret"]!;
    ///     options.RequestRefreshToken = true;
    /// });
    /// </code>
    /// </example>
    public static AuthenticationBuilder AddKyrolusGoogleAuth(
        this IServiceCollection services,
        Action<KyrolusGoogleAuthOptions> configure,
        Action<GoogleOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusGoogleAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds Google OAuth 2.0 / OpenID Connect authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus Google options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusGoogleAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusGoogleAuthOptions> configure,
        Action<GoogleOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusGoogleAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.Google;
        var isConfigured = !string.IsNullOrWhiteSpace(options.ClientId)
                           && !string.IsNullOrWhiteSpace(options.ClientSecret);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(options, provider, isConfigured, "ClientId, ClientSecret");

        var scheme = options.ResolveScheme(provider);

        builder.AddGoogle(scheme, options.ResolveDisplayName(provider), google =>
        {
            google.ClientId = options.ClientId;
            google.ClientSecret = options.ClientSecret;

            if (options.RequestRefreshToken)
            {
                google.AccessType = "offline";
            }

            // hd and prompt are authorization-request parameters. Routing them through
            // AdditionalAuthorizationParameters lets the handler encode them properly; the old
            // approach of appending "?hd=..." to AuthorizationEndpoint produced a malformed URL
            // and silently skipped encoding.
            if (!string.IsNullOrWhiteSpace(options.HostedDomain))
            {
                google.AdditionalAuthorizationParameters["hd"] = options.HostedDomain;
            }

            if (!string.IsNullOrWhiteSpace(options.Prompt))
            {
                google.AdditionalAuthorizationParameters["prompt"] = options.Prompt;
            }

            KyrolusExternalAuthConfigurator.Apply(google, options, provider);

            configureProvider?.Invoke(google);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
