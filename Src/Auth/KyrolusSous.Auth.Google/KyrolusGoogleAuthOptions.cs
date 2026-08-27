using KyrolusSous.Auth.Abstractions;

namespace KyrolusSous.Auth.Google;

/// <summary>
/// Configuration options for Google OAuth 2.0 / OpenID Connect authentication.
/// </summary>
public sealed class KyrolusGoogleAuthOptions : KyrolusExternalLoginOptions
{
    /// <summary>
    /// Initializes a new instance seeded with the standard OpenID Connect scopes.
    /// </summary>
    public KyrolusGoogleAuthOptions()
    {
        Scopes = ["openid", "profile", "email"];
    }

    /// <summary>
    /// Gets or sets the Google OAuth 2.0 Client ID.
    /// </summary>
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Gets or sets the Google OAuth 2.0 Client Secret.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets the Google Workspace domain to restrict authentication to (the <c>hd</c>
    /// parameter). Leave <c>null</c> to allow any Google account.
    /// </summary>
    /// <remarks>
    /// Google treats <c>hd</c> as a hint, not a guarantee - a crafted request can drop it. Always
    /// re-check the <c>hd</c> claim on the returned identity before trusting the restriction.
    /// </remarks>
    public string? HostedDomain { get; set; }

    /// <summary>
    /// Gets or sets whether to request offline access, which is what makes Google return a refresh
    /// token. Defaults to <c>false</c>.
    /// </summary>
    public bool RequestRefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>prompt</c> parameter (<c>none</c>, <c>consent</c>, <c>select_account</c>).
    /// Google only issues a refresh token on the first consent, so pair
    /// <see cref="RequestRefreshToken"/> with <c>"consent"</c> when a refresh token is required
    /// on every sign-in.
    /// </summary>
    public string? Prompt { get; set; }
}
