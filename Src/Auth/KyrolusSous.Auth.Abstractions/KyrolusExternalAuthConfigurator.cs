using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Applies the shared <see cref="KyrolusExternalLoginOptions"/> settings to any OAuth handler and
/// wires the <see cref="IKyrolusExternalLoginHandler"/> pipeline. Every Kyrolus provider package
/// routes through here, so a fix or a feature lands in all of them at once.
/// </summary>
public static class KyrolusExternalAuthConfigurator
{
    /// <summary>
    /// Copies the provider-neutral settings from <paramref name="source"/> onto
    /// <paramref name="target"/> and installs the external-login ticket handler.
    /// </summary>
    /// <typeparam name="TOptions">The concrete OAuth options type of the provider.</typeparam>
    /// <param name="target">The provider options instance being configured.</param>
    /// <param name="source">The Kyrolus options the caller supplied.</param>
    /// <param name="providerName">The canonical provider name, from <see cref="KyrolusAuthConstants.Providers"/>.</param>
    /// <remarks>
    /// The external-login pipeline is installed by chaining onto
    /// <c>Events.OnCreatingTicket</c>. Replacing <c>Events</c> wholesale, or reassigning
    /// <c>OnCreatingTicket</c>, from a provider escape hatch that runs afterwards removes it.
    /// </remarks>
    public static void Apply<TOptions>(TOptions target, KyrolusExternalLoginOptions source, string providerName)
        where TOptions : OAuthOptions
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        target.SaveTokens = source.SaveTokens;
        target.BackchannelTimeout = source.BackchannelTimeout;

        if (!string.IsNullOrWhiteSpace(source.CallbackPath))
        {
            target.CallbackPath = source.CallbackPath;
        }

        foreach (var scope in source.Scopes)
        {
            if (!string.IsNullOrWhiteSpace(scope) && !target.Scope.Contains(scope))
            {
                target.Scope.Add(scope);
            }
        }

        foreach (var parameter in source.AdditionalAuthorizationParameters)
        {
            target.AdditionalAuthorizationParameters[parameter.Key] = parameter.Value;
        }

        foreach (var mapping in source.ClaimMappings)
        {
            target.ClaimActions.MapJsonKey(mapping.InternalClaimType, mapping.ExternalClaimType);
        }

        InstallExternalLoginHandler(target, source, providerName);
    }

    /// <summary>
    /// Guards against a misconfiguration that only shows up as a mysterious 404 at sign-in time:
    /// registering the same provider under a second scheme while both schemes keep the single
    /// default callback path of that provider.
    /// </summary>
    /// <param name="options">The Kyrolus options the caller supplied.</param>
    /// <param name="providerName">The canonical provider name.</param>
    public static void ValidateSchemeAndCallback(KyrolusExternalLoginOptions options, string providerName)
    {
        ArgumentNullException.ThrowIfNull(options);

        var scheme = options.ResolveScheme(providerName);
        if (!string.Equals(scheme, providerName, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(options.CallbackPath))
        {
            throw new InvalidOperationException(
                $"The {providerName} provider is registered under the custom scheme '{scheme}' but no " +
                $"{nameof(KyrolusExternalLoginOptions.CallbackPath)} was set. Two schemes cannot share the " +
                "same default callback path - give each one its own path.");
        }
    }

    /// <summary>
    /// Throws when a provider was registered without the credentials it needs, unless the caller
    /// opted out via <see cref="KyrolusExternalLoginOptions.ThrowIfNotConfigured"/>.
    /// </summary>
    /// <param name="options">The Kyrolus options the caller supplied.</param>
    /// <param name="providerName">The canonical provider name.</param>
    /// <param name="isConfigured">Whether the required credentials are present.</param>
    /// <param name="requiredSettings">The setting names the provider needs, for the error message.</param>
    public static void ValidateConfigured(
        KyrolusExternalLoginOptions options,
        string providerName,
        bool isConfigured,
        string requiredSettings)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (isConfigured || !options.ThrowIfNotConfigured)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{providerName} authentication is missing required configuration ({requiredSettings}). " +
            $"Set them, or set {nameof(KyrolusExternalLoginOptions.ThrowIfNotConfigured)} = false to register " +
            "the provider in a deliberately disabled state.");
    }

    private static void InstallExternalLoginHandler<TOptions>(
        TOptions target,
        KyrolusExternalLoginOptions source,
        string providerName)
        where TOptions : OAuthOptions
    {
        // Chained, not replaced: whatever the caller wired up still runs.
        var previous = target.Events.OnCreatingTicket;

        target.Events.OnCreatingTicket = async context =>
        {
            await previous(context).ConfigureAwait(false);

            var info = CreateLoginInfo(context, providerName);

            if (string.IsNullOrWhiteSpace(info.ProviderKey))
            {
                throw new KyrolusExternalLoginException(
                    providerName,
                    KyrolusAuthConstants.Errors.ExternalLoginFailed,
                    $"{providerName} did not report a valid user identifier (sub/id).");
            }

            if (source.RequireVerifiedEmail && !info.EmailVerified)
            {
                throw new KyrolusExternalLoginException(
                    providerName,
                    KyrolusAuthConstants.Errors.ExternalLoginDenied,
                    $"{providerName} did not report a verified email address for this account.");
            }

            var handler = context.HttpContext.RequestServices.GetService<IKyrolusExternalLoginHandler>();
            if (handler is null)
            {
                // No application hook registered: the principal from the provider is the result.
                StampProviderClaims(context.Principal, info);
                return;
            }

            var result = await handler
                .HandleAsync(info, source, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                // Throwing is what actually stops the sign-in - see KyrolusExternalLoginException.
                throw new KyrolusExternalLoginException(
                    providerName,
                    result.ErrorCode ?? KyrolusAuthConstants.Errors.ExternalLoginFailed,
                    result.ErrorDescription ?? "The external login was refused.");
            }

            if (result.Principal is not null)
            {
                context.Principal = result.Principal;
            }
            else if (result.AdditionalClaims.Count > 0 &&
                     context.Principal?.Identities.FirstOrDefault() is { } identity)
            {
                identity.AddClaims(result.AdditionalClaims);
            }

            StampProviderClaims(context.Principal, info);
        };
    }

    /// <summary>
    /// Records which provider issued the identity, so downstream code and the local login record
    /// never have to guess.
    /// </summary>
    private static void StampProviderClaims(ClaimsPrincipal? principal, KyrolusExternalLoginInfo info)
    {
        if (principal?.Identities.FirstOrDefault() is not { } identity)
        {
            return;
        }

        if (!identity.HasClaim(c => c.Type == KyrolusAuthConstants.Claims.Provider))
        {
            identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.Provider, info.ProviderName));
        }

        if (info.ProviderKey.Length > 0 &&
            !identity.HasClaim(c => c.Type == KyrolusAuthConstants.Claims.ProviderKey))
        {
            identity.AddClaim(new Claim(KyrolusAuthConstants.Claims.ProviderKey, info.ProviderKey));
        }
    }

    /// <summary>
    /// Normalises the principal from the provider into a shape the application can consume
    /// without a per-provider switch statement.
    /// </summary>
    private static KyrolusExternalLoginInfo CreateLoginInfo(
        OAuthCreatingTicketContext context,
        string providerName)
    {
        var principal = context.Principal ?? new ClaimsPrincipal(new ClaimsIdentity());

        var tokens = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (context.Properties is not null)
        {
            // Populated by the handler before the ticket is created, when SaveTokens is on.
            foreach (var token in context.Properties.GetTokens())
            {
                tokens[token.Name] = token.Value;
            }
        }

        if (!string.IsNullOrEmpty(context.AccessToken))
        {
            tokens[KyrolusAuthConstants.Tokens.AccessToken] = context.AccessToken;
        }

        if (!string.IsNullOrEmpty(context.RefreshToken))
        {
            tokens[KyrolusAuthConstants.Tokens.RefreshToken] = context.RefreshToken;
        }

        return new KyrolusExternalLoginInfo
        {
            ProviderName = providerName,
            ProviderKey = First(principal, ClaimTypes.NameIdentifier, KyrolusAuthConstants.Claims.Sub, "id") ?? "",
            Principal = principal,
            Email = First(principal, ClaimTypes.Email, KyrolusAuthConstants.Claims.Email),
            EmailVerified = IsTrue(First(principal, KyrolusAuthConstants.Claims.EmailVerified)),
            DisplayName = First(principal, ClaimTypes.Name, KyrolusAuthConstants.Claims.Name),
            GivenName = First(principal, ClaimTypes.GivenName, KyrolusAuthConstants.Claims.GivenName),
            FamilyName = First(principal, ClaimTypes.Surname, KyrolusAuthConstants.Claims.FamilyName),
            PictureUrl = First(principal, KyrolusAuthConstants.Claims.Picture, "urn:google:picture", "avatar_url"),
            Locale = First(principal, KyrolusAuthConstants.Claims.Locale, "urn:google:locale"),
            Tokens = tokens,
        };
    }

    private static string? First(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    // Providers disagree here: some send a JSON boolean, others the string "true", GitHub "True".
    // bool.TryParse handles all three and rejects anything else.
    private static bool IsTrue(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
