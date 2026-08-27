using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Facebook;

/// <summary>
/// Extension methods for configuring Kyrolus Facebook Authentication.
/// </summary>
public static class FacebookAuthExtensions
{
    /// <summary>
    /// Adds Facebook Login authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus Facebook options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="FacebookOptions"/>.
    /// </param>
    public static AuthenticationBuilder AddKyrolusFacebookAuth(
        this IServiceCollection services,
        Action<KyrolusFacebookAuthOptions> configure,
        Action<FacebookOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusFacebookAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds Facebook Login authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus Facebook options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusFacebookAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusFacebookAuthOptions> configure,
        Action<FacebookOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusFacebookAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.Facebook;
        var isConfigured = !string.IsNullOrWhiteSpace(options.AppId)
                           && !string.IsNullOrWhiteSpace(options.AppSecret);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(options, provider, isConfigured, "AppId, AppSecret");

        var scheme = options.ResolveScheme(provider);

        builder.AddFacebook(scheme, options.ResolveDisplayName(provider), facebook =>
        {
            facebook.AppId = options.AppId;
            facebook.AppSecret = options.AppSecret;
            facebook.SendAppSecretProof = options.SendAppSecretProof;

            foreach (var field in options.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field) && !facebook.Fields.Contains(field))
                {
                    facebook.Fields.Add(field);
                }
            }

            KyrolusExternalAuthConfigurator.Apply(facebook, options, provider);

            configureProvider?.Invoke(facebook);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
