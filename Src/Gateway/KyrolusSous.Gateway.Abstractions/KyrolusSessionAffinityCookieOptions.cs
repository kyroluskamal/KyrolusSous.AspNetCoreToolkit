namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Hardened security configuration options for session affinity cookies (Sticky Sessions).
/// Defends against XSS, CSRF, and MITM interception attacks.
/// </summary>
public sealed record KyrolusSessionAffinityCookieOptions
{
    /// <summary>
    /// Gets or sets the path for the affinity cookie. Defaults to <c>"/"</c>.
    /// </summary>
    public string? Path { get; init; } = "/";

    /// <summary>
    /// Gets or sets the domain for the affinity cookie.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets or sets whether the affinity cookie is accessible only via HTTP/S and blocked from client-side scripts.
    /// Defends against Cross-Site Scripting (XSS) cookie theft. Defaults to <c>true</c>.
    /// </summary>
    public bool HttpOnly { get; init; } = true;

    /// <summary>
    /// Gets or sets the cookie transmission security policy (<c>"Always"</c>, <c>"SameAsRequest"</c>, <c>"None"</c>).
    /// Defaults to <c>"SameAsRequest"</c> to defend against man-in-the-middle sniffing over unencrypted connections.
    /// </summary>
    public string? SecurePolicy { get; init; } = "SameAsRequest";

    /// <summary>
    /// Gets or sets the SameSite behavior (<c>"Lax"</c>, <c>"Strict"</c>, <c>"None"</c>).
    /// Defends against Cross-Site Request Forgery (CSRF). Defaults to <c>"Lax"</c>.
    /// </summary>
    public string? SameSite { get; init; } = "Lax";

    /// <summary>
    /// Gets or sets the cookie expiration duration.
    /// </summary>
    public TimeSpan? Expiration { get; init; }

    /// <summary>
    /// Gets or sets the maximum age duration of the cookie.
    /// </summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// Gets or sets whether the cookie is essential for the application to function (GDPR compliance).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool IsEssential { get; init; } = true;
}
