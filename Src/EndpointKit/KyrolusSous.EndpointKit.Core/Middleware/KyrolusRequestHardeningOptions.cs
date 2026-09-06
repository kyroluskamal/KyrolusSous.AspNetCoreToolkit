namespace KyrolusSous.EndpointKit.Core.Middleware;

/// <summary>
/// Configuration options for <see cref="KyrolusRequestHardeningMiddleware"/> defending standalone APIs
/// against path traversal, HTTP method override spoofing, client certificate header tampering, and header flood DoS attacks.
/// </summary>
public sealed class KyrolusRequestHardeningOptions
{
    /// <summary>
    /// Gets or sets the maximum allowed number of HTTP request headers.
    /// Requests exceeding this limit receive HTTP 431 Request Header Fields Too Large.
    /// Defaults to <c>100</c>.
    /// </summary>
    public int MaxHeaderCount { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum allowed total size (in bytes) of all HTTP request header names and values combined.
    /// Requests exceeding this limit receive HTTP 431 Request Header Fields Too Large.
    /// Defaults to <c>32768</c> (32 KB).
    /// </summary>
    public int MaxTotalHeaderSizeBytes { get; set; } = 32 * 1024;

    /// <summary>
    /// Gets or sets whether path traversal sequences (e.g. <c>..</c>, <c>..%2f</c>) and null-byte injection (<c>%00</c>)
    /// should be blocked with HTTP 400 Bad Request.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool BlockPathTraversal { get; set; } = true;

    /// <summary>
    /// Gets or sets whether HTTP method override headers on safe HTTP verbs (GET, HEAD, OPTIONS)
    /// attempting to transform them into unsafe verbs (POST, PUT, DELETE, PATCH) should be blocked with HTTP 405 Method Not Allowed.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool BlockSafeVerbMethodOverride { get; set; } = true;

    /// <summary>
    /// Gets or sets whether method override headers (<c>X-HTTP-Method-Override</c>, <c>X-HTTP-Method</c>, <c>X-Method-Override</c>)
    /// should be stripped from incoming requests to prevent confusing downstream business logic.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool StripMethodOverrideHeaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether untrusted client certificate headers (<c>X-Client-Cert*</c>)
    /// should be stripped from incoming requests to defend against mTLS header spoofing.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool StripUntrustedClientCertHeaders { get; set; } = true;
}
