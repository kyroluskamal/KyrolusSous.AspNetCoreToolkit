using KyrolusSous.Auth.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.MicrosoftAccount;

/// <summary>
/// Extension methods for configuring Kyrolus Microsoft Account / Entra ID authentication.
/// </summary>
public static class MicrosoftAuthExtensions
{
    /// <summary>
    /// Adds Microsoft Account / Microsoft Entra ID authentication to the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the Kyrolus Microsoft options.</param>
    /// <param name="configureProvider">
    /// Optional escape hatch applied last, giving direct access to the underlying
    /// <see cref="MicrosoftAccountOptions"/>.
    /// </param>
    /// <example>
    /// <code>
    /// services.AddKyrolusMicrosoftAuth(options =>
    /// {
    ///     options.ClientId = configuration["Auth:Microsoft:ClientId"]!;
    ///     options.ClientSecret = configuration["Auth:Microsoft:ClientSecret"]!;
    ///     options.Tenant = "contoso.onmicrosoft.com";   // single-tenant line-of-business app
    /// });
    /// </code>
    /// </example>
    public static AuthenticationBuilder AddKyrolusMicrosoftAuth(
        this IServiceCollection services,
        Action<KyrolusMicrosoftAuthOptions> configure,
        Action<MicrosoftAccountOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddAuthentication().AddKyrolusMicrosoftAuth(configure, configureProvider);
    }

    /// <summary>
    /// Adds Microsoft Account / Microsoft Entra ID authentication to an existing authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Configures the Kyrolus Microsoft options.</param>
    /// <param name="configureProvider">Optional escape hatch applied last.</param>
    public static AuthenticationBuilder AddKyrolusMicrosoftAuth(
        this AuthenticationBuilder builder,
        Action<KyrolusMicrosoftAuthOptions> configure,
        Action<MicrosoftAccountOptions>? configureProvider = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KyrolusMicrosoftAuthOptions();
        configure(options);

        const string provider = KyrolusAuthConstants.Providers.Microsoft;
        var isConfigured = !string.IsNullOrWhiteSpace(options.ClientId)
                           && !string.IsNullOrWhiteSpace(options.ClientSecret);

        KyrolusExternalAuthConfigurator.ValidateSchemeAndCallback(options, provider);
        KyrolusExternalAuthConfigurator.ValidateConfigured(options, provider, isConfigured, "ClientId, ClientSecret");

        var scheme = options.ResolveScheme(provider);

        builder.AddMicrosoftAccount(scheme, options.ResolveDisplayName(provider), microsoft =>
        {
            microsoft.ClientId = options.ClientId;
            microsoft.ClientSecret = options.ClientSecret;

            // The tenant is part of the endpoint path, not a query parameter. Pointing the
            // handler at the tenant-specific authority is what actually stops accounts from
            // other tenants signing in - a hint parameter would not.
            var tenant = string.IsNullOrWhiteSpace(options.Tenant) ? "common" : options.Tenant.Trim();
            if (!string.Equals(tenant, "common", StringComparison.OrdinalIgnoreCase))
            {
                microsoft.AuthorizationEndpoint =
                    $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/authorize";
                microsoft.TokenEndpoint =
                    $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/token";
            }

            if (!string.IsNullOrWhiteSpace(options.Prompt))
            {
                microsoft.AdditionalAuthorizationParameters["prompt"] = options.Prompt;
            }

            if (!string.IsNullOrWhiteSpace(options.DomainHint))
            {
                microsoft.AdditionalAuthorizationParameters["domain_hint"] = options.DomainHint;
            }

            KyrolusExternalAuthConfigurator.Apply(microsoft, options, provider);

            configureProvider?.Invoke(microsoft);
        });

        builder.Services.AddSingleton<IKyrolusExternalAuthProvider>(
            new KyrolusExternalAuthProviderDescriptor(
                provider, scheme, options.ResolveDisplayName(provider), isConfigured));

        return builder;
    }
}
