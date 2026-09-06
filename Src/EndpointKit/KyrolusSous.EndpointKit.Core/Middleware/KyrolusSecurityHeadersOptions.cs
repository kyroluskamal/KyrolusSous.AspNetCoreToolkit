namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Configuration options for HTTP security response headers.
/// </summary>
public sealed class KyrolusSecurityHeadersOptions
{
    /// <summary>
    /// Gets or sets the value for X-Content-Type-Options header. Defaults to "nosniff".
    /// Set to null to suppress sending this header.
    /// </summary>
    public string? ContentTypeOptions { get; set; } = "nosniff";

    /// <summary>
    /// Gets or sets the value for X-Frame-Options header. Defaults to "DENY".
    /// Set to null to suppress sending this header.
    /// </summary>
    public string? FrameOptions { get; set; } = "DENY";

    /// <summary>
    /// Gets or sets the value for X-XSS-Protection header. Defaults to "1; mode=block".
    /// Set to null to suppress sending this header.
    /// </summary>
    public string? XssProtection { get; set; } = "1; mode=block";

    /// <summary>
    /// Gets or sets the value for Referrer-Policy header. Defaults to "strict-origin-when-cross-origin".
    /// Set to null to suppress sending this header.
    /// </summary>
    public string? ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Gets or sets an optional Content-Security-Policy (CSP) header value. Defaults to null (not sent unless specified).
    /// </summary>
    public string? ContentSecurityPolicy { get; set; }

    /// <summary>
    /// Gets or sets an optional Permissions-Policy header value. Defaults to null (not sent unless specified).
    /// </summary>
    public string? PermissionsPolicy { get; set; }

    /// <summary>
    /// Gets or sets the Strict-Transport-Security (HSTS) header value applied to HTTPS responses.
    /// Defaults to <c>"max-age=31536000; includeSubDomains"</c>.
    /// Set to null to disable HSTS header injection.
    /// </summary>
    public string? StrictTransportSecurity { get; set; } = "max-age=31536000; includeSubDomains";

    /// <summary>
    /// Gets or sets whether server information disclosure headers (e.g. Server, X-Powered-By, X-AspNet-Version)
    /// should be automatically stripped from outbound HTTP responses. Defaults to <c>true</c>.
    /// </summary>
    public bool RemoveServerHeaders { get; set; } = true;

    /// <summary>
    /// Gets the collection of additional custom response header names to strip before response transmission.
    /// </summary>
    public HashSet<string> CustomHeadersToRemove { get; } = new(StringComparer.OrdinalIgnoreCase);
}
