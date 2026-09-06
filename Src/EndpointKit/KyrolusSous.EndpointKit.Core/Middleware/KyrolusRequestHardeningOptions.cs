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
    /// Gets or sets whether the incoming request query string should also be inspected for path traversal sequences and null-byte injection.
    /// Defends against query-based file inclusion and directory traversal attacks (CWE-22).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool InspectQueryStringForTraversal { get; set; } = true;

    /// <summary>
    /// Gets or sets whether HTTP request smuggling anomalies (conflicting Transfer-Encoding / Content-Length headers,
    /// duplicate differing Content-Length headers, or malformed framing control characters) should be blocked with HTTP 400 Bad Request.
    /// Defends against HTTP Request Smuggling (CWE-444 / RFC 7230 § 3.3.3 / RFC 9112 § 6.1).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool BlockRequestSmuggling { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional maximum allowed request body size in bytes.
    /// If configured and the incoming <c>Content-Length</c> header exceeds this threshold, the request is immediately rejected with HTTP 413 Payload Too Large.
    /// Defaults to <c>null</c> (unrestricted by this middleware).
    /// </summary>
    public long? MaxRequestBodySizeBytes { get; set; }

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

    /// <summary>
    /// Gets or sets whether dangerous HTTP verbs (<c>TRACE</c>, <c>TRACK</c>, <c>CONNECT</c>) should be blocked with HTTP 405 Method Not Allowed.
    /// Defends against Cross-Site Tracing (XST - CWE-693) and unauthorized proxy tunneling.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool BlockDangerousVerbs { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional list of allowed incoming HTTP Host header values.
    /// If configured and the request's <c>Host</c> header does not match any entry, the request is rejected with HTTP 400 Bad Request.
    /// Defends against Host Header Poisoning / Password Reset Poisoning (CWE-644).
    /// Defaults to <c>null</c> (unrestricted).
    /// </summary>
    public IReadOnlyList<string>? AllowedHosts { get; set; }

    /// <summary>
    /// Gets or sets an optional allowlist of permitted client IPv4/IPv6 addresses or CIDR blocks (e.g. <c>"10.0.0.0/8"</c>).
    /// If configured, any caller connecting from an unlisted IP is rejected with HTTP 403 Forbidden.
    /// Defaults to <c>null</c>.
    /// </summary>
    public IReadOnlyList<string>? AllowedIpsOrCidrs { get; set; }

    /// <summary>
    /// Gets or sets an optional blocklist of denied client IPv4/IPv6 addresses or CIDR blocks (e.g. <c>"203.0.113.50"</c>).
    /// If configured, any caller connecting from a blocked IP is rejected with HTTP 403 Forbidden.
    /// Defaults to <c>null</c>.
    /// </summary>
    public IReadOnlyList<string>? BlockedIpsOrCidrs { get; set; }

    /// <summary>
    /// Gets or sets an optional list of allowed incoming request Content-Type MIME types (e.g. <c>"application/json"</c>, <c>"text/plain"</c>).
    /// If configured and a request with content has an unsupported Content-Type, it is rejected with HTTP 415 Unsupported Media Type.
    /// Defends against XXE and unexpected deserialization payload attacks (CWE-436 / RFC 7231).
    /// Defaults to <c>null</c> (unrestricted).
    /// </summary>
    public IReadOnlyList<string>? AllowedContentTypes { get; set; }
}
