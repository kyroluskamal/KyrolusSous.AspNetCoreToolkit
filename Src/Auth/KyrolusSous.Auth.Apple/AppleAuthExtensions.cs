using AspNet.Security.OAuth.Apple;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Apple;

/// <summary>
/// Extension methods for configuring Kyrolus Sign in with Apple.
/// </summary>
public static class AppleAuthExtensions
{
    /// <summary>
    /// Adds Sign in with Apple authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus Apple options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="AppleAuthenticationOptions"/>.
    /// </param>
    /// <example>
    /// <code>
    /// services.AddKyrolusAppleAuth(options =>
    /// {
    ///     options.ClientId = "com.contoso.web";      // the Service ID, not the bundle ID
    ///     options.TeamId = "ABCDE12345";
    ///     options.KeyId = "K1234ABCD5";
    ///     options.PrivateKeyPath = "/run/secrets/AuthKey_K1234ABCD5.p8";
    /// });
    /// </code>
    /// </example>
    public static AuthenticationBuilder AddKyrolusAppleAuth(
        this IServiceCollection services,
        Action<KyrolusAppleAuthOptions> configure,
        Action<AppleAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusAppleAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds Sign in with Apple authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus Apple options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusAppleAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusAppleAuthOptions> configure,
        Action<AppleAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusAppleAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.Apple;

        var hasPath = !string.IsNullOrWhiteSpace(options.PrivateKeyPath);
        var hasPem = !string.IsNullOrWhiteSpace(options.PrivateKeyPem);

        if (hasPath && hasPem)
        {
            throw new InvalidOperationException(
                $"Set either {nameof(KyrolusAppleAuthOptions.PrivateKeyPath)} or " +
                $"{nameof(KyrolusAppleAuthOptions.PrivateKeyPem)} for Apple authentication, not both.");
        }

        // Apple has no static client secret: the handler mints a short-lived JWT signed with the
        // .p8 key on every refresh. Without a key there is nothing to sign with, so the provider
        // is not usable however complete the rest of the configuration looks.
        var isConfigured = !string.IsNullOrWhiteSpace(options.ClientId)
                           && !string.IsNullOrWhiteSpace(options.TeamId)
                           && !string.IsNullOrWhiteSpace(options.KeyId)
                           && (hasPath || hasPem);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(
            options, provider, isConfigured, "ClientId (Service ID), TeamId, KeyId, and PrivateKeyPath or PrivateKeyPem");

        if (options.ClientSecretExpiresAfter > TimeSpan.FromDays(180))
        {
            throw new InvalidOperationException(
                $"{nameof(KyrolusAppleAuthOptions.ClientSecretExpiresAfter)} cannot exceed 6 months; " +
                "Apple rejects client secrets with a longer lifetime.");
        }

        var scheme = options.ResolveScheme(provider);

        builder.AddApple(scheme, options.ResolveDisplayName(provider), apple =>
        {
            apple.ClientId = options.ClientId;
            apple.TeamId = options.TeamId;
            apple.KeyId = options.KeyId;
            apple.ClientSecretExpiresAfter = options.ClientSecretExpiresAfter;
            apple.ValidateTokens = options.ValidateTokens;

            if (hasPath || hasPem)
            {
                // GenerateClientSecret has to be on, otherwise the handler sends the (empty)
                // static ClientSecret and Apple rejects every token exchange with invalid_client.
                apple.GenerateClientSecret = true;

                var keyFile = hasPath
                    ? KyrolusApplePrivateKeyFile.FromPath(options.PrivateKeyPath!)
                    : KyrolusApplePrivateKeyFile.FromPem(options.PrivateKeyPem!);

                apple.UsePrivateKey(_ => keyFile);
            }

            KyrolusExternalAuthConfigurator.Apply(apple, options, provider);

            configureProvider?.Invoke(apple);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
