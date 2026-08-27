namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Base configuration options shared by every Kyrolus external login provider.
/// </summary>
public abstract class KyrolusExternalLoginOptions
{
    /// <summary>
    /// Gets or sets the authentication scheme name. Defaults to the provider name
    /// (for example <c>"Google"</c>). Override it when the same provider is registered twice
    /// (multi-tenant setups with different client credentials per tenant).
    /// </summary>
    public string? SchemeName { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name shown on a sign-in button.
    /// Defaults to the provider name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically create a local user account on first external login.
    /// Only honoured when an <see cref="IKyrolusExternalLoginHandler"/> that supports provisioning
    /// is registered (the default handler in <c>KyrolusSous.Auth.Runtime</c> does).
    /// Defaults to <c>false</c> (opt-in).
    /// </summary>
    public bool AutoCreateUser { get; set; }

    /// <summary>
    /// Gets or sets the default role assigned to auto-created users.
    /// Only effective when <see cref="AutoCreateUser"/> is <c>true</c>.
    /// Defaults to <c>"User"</c>.
    /// </summary>
    public string DefaultRole { get; set; } = "User";

    /// <summary>
    /// Gets or sets whether an external identity whose email is already registered locally is
    /// linked to that existing account. Leaving this <c>false</c> is the safe default: a provider
    /// that does not verify email addresses would otherwise allow account takeover.
    /// Only honoured when the external identity reports a <em>verified</em> email.
    /// </summary>
    public bool LinkToExistingUserByEmail { get; set; }

    /// <summary>
    /// Gets or sets the default tenant identifier assigned to auto-created users in multi-tenant environments.
    /// </summary>
    public string? DefaultTenantId { get; set; }

    /// <summary>
    /// Gets or sets whether the external identity must report a verified email address.
    /// Defaults to <c>false</c>; set to <c>true</c> to reject unverified accounts outright.
    /// </summary>
    public bool RequireVerifiedEmail { get; set; }

    /// <summary>
    /// Gets or sets additional claim mappings from the external provider's JSON payload
    /// to internal claim types.
    /// </summary>
    public IList<KyrolusClaimMapping> ClaimMappings { get; set; } = [];

    /// <summary>
    /// Gets or sets the OAuth scopes to request. Each provider seeds its own sensible defaults;
    /// assign a new list to replace them outright, or call <c>Add</c> to extend them.
    /// </summary>
    public IList<string> Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets extra parameters appended to the provider's authorization request
    /// (for example Google's <c>hd</c> or <c>prompt</c>).
    /// </summary>
    public IDictionary<string, string> AdditionalAuthorizationParameters { get; set; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether to persist external tokens (access, refresh, id) in the
    /// authentication properties. Defaults to <c>true</c>.
    /// </summary>
    public bool SaveTokens { get; set; } = true;

    /// <summary>
    /// Gets or sets the callback path for the external authentication handler.
    /// Leave <c>null</c> to keep the provider default (for example <c>"/signin-google"</c>).
    /// </summary>
    public string? CallbackPath { get; set; }

    /// <summary>
    /// Gets or sets the timeout applied to the provider's back-channel token and userinfo calls.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan BackchannelTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether registration fails fast when the provider's credentials are missing.
    /// Defaults to <c>true</c>: a silently unconfigured provider is a production incident waiting
    /// to happen, and the failure is far cheaper at startup than on a user's first sign-in attempt.
    /// </summary>
    public bool ThrowIfNotConfigured { get; set; } = true;

    /// <summary>
    /// Gets the effective authentication scheme name for this provider.
    /// </summary>
    /// <param name="providerName">The provider's canonical name from <see cref="KyrolusAuthConstants.Providers"/>.</param>
    public string ResolveScheme(string providerName)
        => string.IsNullOrWhiteSpace(SchemeName) ? providerName : SchemeName;

    /// <summary>
    /// Gets the effective display name for this provider.
    /// </summary>
    /// <param name="providerName">The provider's canonical name from <see cref="KyrolusAuthConstants.Providers"/>.</param>
    public string ResolveDisplayName(string providerName)
        => string.IsNullOrWhiteSpace(DisplayName) ? providerName : DisplayName;
}
