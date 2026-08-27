using KyrolusSous.Auth.OpenIddict.Options;

namespace KyrolusSous.Auth.OpenIddict.Config;

/// <summary>
/// Eager startup validation for the OpenIddict options.
/// </summary>
/// <remarks>
/// These options are consumed while the service collection is being built, so they cannot go
/// through <c>IValidateOptions</c>. Checking them here still moves every one of these mistakes
/// from "tokens behave strangely in production" to "the application refuses to start".
/// </remarks>
internal static class KyrolusOpenIddictOptionsValidator
{
    public static void Validate(KyrolusOpenIddictOptions options)
    {
        var failures = new List<string>();

        ValidateIssuer(options.Issuer, failures);
        ValidateKeys(options, failures);
        ValidateEndpoints(options, failures);
        ValidateLifetimes(options, failures);
        ValidateFlows(options, failures);

        Throw(failures, nameof(KyrolusOpenIddictOptions));
    }

    public static void Validate(KyrolusOpenIddictApiOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"{nameof(KyrolusOpenIddictApiOptions.Issuer)} is required.");
        }
        else
        {
            ValidateIssuer(options.Issuer, failures);
        }

        if (options.ValidationMode == KyrolusTokenValidationMode.Introspection)
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                failures.Add(
                    $"{nameof(KyrolusOpenIddictApiOptions.ClientId)} is required when " +
                    $"{nameof(KyrolusOpenIddictApiOptions.ValidationMode)} is Introspection.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                failures.Add(
                    $"{nameof(KyrolusOpenIddictApiOptions.ClientSecret)} is required when " +
                    $"{nameof(KyrolusOpenIddictApiOptions.ValidationMode)} is Introspection.");
            }
        }

        Throw(failures, nameof(KyrolusOpenIddictApiOptions));
    }

    private static void ValidateIssuer(string? issuer, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return;
        }

        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var uri))
        {
            failures.Add($"Issuer '{issuer}' is not an absolute URI.");
            return;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            failures.Add($"Issuer '{issuer}' must not carry a query string or fragment.");
        }
    }

    private static void ValidateKeys(KyrolusOpenIddictOptions options, List<string> failures)
    {
        var hasExplicitCertificates =
            options.SigningCertificate.IsConfigured || options.EncryptionCertificate.IsConfigured;

        if (options.UseDevelopmentKeys && options.UseEphemeralKeys)
        {
            failures.Add(
                $"{nameof(KyrolusOpenIddictOptions.UseDevelopmentKeys)} and " +
                $"{nameof(KyrolusOpenIddictOptions.UseEphemeralKeys)} cannot both be enabled.");
        }

        if ((options.UseDevelopmentKeys || options.UseEphemeralKeys) && hasExplicitCertificates)
        {
            failures.Add(
                "Development or ephemeral keys were requested alongside explicit certificates. " +
                "Pick one: the generated keys would silently win and the configured certificate " +
                "would never be used.");
        }

        if (!options.UseDevelopmentKeys && !options.UseEphemeralKeys && !options.SigningCertificate.IsConfigured)
        {
            failures.Add(
                $"No signing key is configured. Set {nameof(KyrolusOpenIddictOptions.SigningCertificate)}, " +
                $"or {nameof(KyrolusOpenIddictOptions.UseDevelopmentKeys)} for local development.");
        }
    }

    private static void ValidateEndpoints(KyrolusOpenIddictOptions options, List<string> failures)
    {
        RequireRootedPath(options.TokenEndpoint, nameof(KyrolusOpenIddictOptions.TokenEndpoint), failures);
        RequireRootedPath(options.IntrospectionEndpoint, nameof(KyrolusOpenIddictOptions.IntrospectionEndpoint), failures);
        RequireRootedPath(options.RevocationEndpoint, nameof(KyrolusOpenIddictOptions.RevocationEndpoint), failures);
        RequireRootedPath(options.UserInfoEndpoint, nameof(KyrolusOpenIddictOptions.UserInfoEndpoint), failures);
        RequireRootedPath(options.EndSessionEndpoint, nameof(KyrolusOpenIddictOptions.EndSessionEndpoint), failures);

        if (options.AllowAuthorizationCodeFlow || options.AllowImplicitFlow ||
            options.AllowHybridFlow || options.AllowNoneFlow)
        {
            RequireRootedPath(
                options.AuthorizationEndpoint, nameof(KyrolusOpenIddictOptions.AuthorizationEndpoint), failures);
        }

        if (options.AllowDeviceAuthorizationFlow)
        {
            RequireRootedPath(
                options.DeviceAuthorizationEndpoint,
                nameof(KyrolusOpenIddictOptions.DeviceAuthorizationEndpoint),
                failures);
            RequireRootedPath(
                options.EndUserVerificationEndpoint,
                nameof(KyrolusOpenIddictOptions.EndUserVerificationEndpoint),
                failures);
        }
    }

    private static void ValidateLifetimes(KyrolusOpenIddictOptions options, List<string> failures)
    {
        RequirePositive(options.AccessTokenLifetime, nameof(KyrolusOpenIddictOptions.AccessTokenLifetime), failures);
        RequirePositive(options.RefreshTokenLifetime, nameof(KyrolusOpenIddictOptions.RefreshTokenLifetime), failures);
        RequirePositive(options.IdentityTokenLifetime, nameof(KyrolusOpenIddictOptions.IdentityTokenLifetime), failures);
        RequirePositive(
            options.AuthorizationCodeLifetime, nameof(KyrolusOpenIddictOptions.AuthorizationCodeLifetime), failures);

        if (options.RefreshTokenLifetime <= options.AccessTokenLifetime)
        {
            failures.Add(
                $"{nameof(KyrolusOpenIddictOptions.RefreshTokenLifetime)} must be longer than " +
                $"{nameof(KyrolusOpenIddictOptions.AccessTokenLifetime)}; otherwise the refresh token " +
                "expires before it is ever needed.");
        }

        if (options.AllowDeviceAuthorizationFlow)
        {
            RequirePositive(options.DeviceCodeLifetime, nameof(KyrolusOpenIddictOptions.DeviceCodeLifetime), failures);
            RequirePositive(options.UserCodeLifetime, nameof(KyrolusOpenIddictOptions.UserCodeLifetime), failures);
        }
    }

    private static void ValidateFlows(KyrolusOpenIddictOptions options, List<string> failures)
    {
        var anyFlow = options.AllowAuthorizationCodeFlow || options.AllowRefreshTokenFlow ||
                      options.AllowClientCredentialsFlow || options.AllowPasswordFlow ||
                      options.AllowImplicitFlow || options.AllowHybridFlow ||
                      options.AllowDeviceAuthorizationFlow || options.AllowNoneFlow ||
                      options.CustomFlows.Count > 0;

        if (!anyFlow)
        {
            failures.Add("No grant type is enabled, so the server could never issue a token.");
        }

        if (options.AllowRefreshTokenFlow &&
            !options.AllowAuthorizationCodeFlow && !options.AllowPasswordFlow &&
            !options.AllowHybridFlow && !options.AllowDeviceAuthorizationFlow)
        {
            failures.Add(
                "The refresh token flow is enabled but no flow that can issue a refresh token is. " +
                "Enable the authorization code, password, hybrid or device flow as well.");
        }

        if (options.UseReferenceAccessTokens && options.DisableTokenStorage)
        {
            failures.Add(
                $"{nameof(KyrolusOpenIddictOptions.UseReferenceAccessTokens)} needs token storage, " +
                $"but {nameof(KyrolusOpenIddictOptions.DisableTokenStorage)} is enabled.");
        }

        if (options.UseReferenceRefreshTokens && options.DisableTokenStorage)
        {
            failures.Add(
                $"{nameof(KyrolusOpenIddictOptions.UseReferenceRefreshTokens)} needs token storage, " +
                $"but {nameof(KyrolusOpenIddictOptions.DisableTokenStorage)} is enabled.");
        }
    }

    private static void RequireRootedPath(string value, string name, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{name} is required.");
        }
        else if (!value.StartsWith('/'))
        {
            failures.Add($"{name} must be a rooted path starting with '/'; got '{value}'.");
        }
    }

    private static void RequirePositive(TimeSpan value, string name, List<string> failures)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"{name} must be greater than zero.");
        }
    }

    private static void Throw(List<string> failures, string optionsName)
    {
        if (failures.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{optionsName} is invalid:{Environment.NewLine}  - " +
            string.Join($"{Environment.NewLine}  - ", failures));
    }
}
