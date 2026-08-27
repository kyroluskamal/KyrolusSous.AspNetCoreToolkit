using AspNet.Security.OAuth.Discord;
using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Discord;

/// <summary>
/// Extension methods for configuring Kyrolus Discord Authentication.
/// </summary>
public static class DiscordAuthExtensions
{
    /// <summary>
    /// Adds Discord OAuth 2.0 authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus Discord options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="DiscordAuthenticationOptions"/>.
    /// </param>
    public static AuthenticationBuilder AddKyrolusDiscordAuth(
        this IServiceCollection services,
        Action<KyrolusDiscordAuthOptions> configure,
        Action<DiscordAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusDiscordAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds Discord OAuth 2.0 authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus Discord options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusDiscordAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusDiscordAuthOptions> configure,
        Action<DiscordAuthenticationOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusDiscordAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.Discord;
        var isConfigured = !string.IsNullOrWhiteSpace(options.ClientId)
                           && !string.IsNullOrWhiteSpace(options.ClientSecret);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(options, provider, isConfigured, "ClientId, ClientSecret");

        var scheme = options.ResolveScheme(provider);

        builder.AddDiscord(scheme, options.ResolveDisplayName(provider), discord =>
        {
            discord.ClientId = options.ClientId;
            discord.ClientSecret = options.ClientSecret;

            if (!string.IsNullOrWhiteSpace(options.Prompt))
            {
                discord.Prompt = options.Prompt;
            }

            KyrolusExternalAuthConfigurator.Apply(discord, options, provider);

            configureProvider?.Invoke(discord);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
