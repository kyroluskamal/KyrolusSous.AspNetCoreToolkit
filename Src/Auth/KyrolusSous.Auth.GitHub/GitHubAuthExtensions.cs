using AspNet.Security.OAuth.GitHub;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.GitHub;

/// <summary>
/// Extension methods for configuring Kyrolus GitHub Authentication.
/// </summary>
public static class GitHubAuthExtensions
{
    /// <summary>
    /// Adds GitHub OAuth authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus GitHub options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="GitHubAuthenticationOptions"/>.
    /// </param>
    public static AuthenticationBuilder AddKyrolusGitHubAuth(
        this IServiceCollection services,
        Action<KyrolusGitHubAuthOptions> configure,
        Action<GitHubAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusGitHubAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds GitHub OAuth authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus GitHub options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusGitHubAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusGitHubAuthOptions> configure,
        Action<GitHubAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusGitHubAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.GitHub;
        var isConfigured = !string.IsNullOrWhiteSpace(options.ClientId)
                           && !string.IsNullOrWhiteSpace(options.ClientSecret);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(options, provider, isConfigured, "ClientId, ClientSecret");

        var scheme = options.ResolveScheme(provider);

        builder.AddGitHub(scheme, options.ResolveDisplayName(provider), github =>
        {
            github.ClientId = options.ClientId;
            github.ClientSecret = options.ClientSecret;

            // The handler rewrites every endpoint (including the /api/v3 API prefix that
            // Enterprise Server uses) from this one property. Setting the endpoints by hand
            // gets the API host wrong: Enterprise Server serves it at {domain}/api/v3, not
            // at api.{domain}.
            if (!string.IsNullOrWhiteSpace(options.EnterpriseDomain))
            {
                github.EnterpriseDomain = options.EnterpriseDomain;
            }

            KyrolusExternalAuthConfigurator.Apply(github, options, provider);

            configureProvider?.Invoke(github);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
