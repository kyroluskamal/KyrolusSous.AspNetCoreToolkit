namespace KyrolusSous.Logging.Core.Middleware;

/// <summary>
/// Configuration options for the enterprise HTTP Request/Response logging middleware.
/// </summary>
public sealed class KyrolusHttpLoggingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to capture and log the HTTP request body. Default is <c>false</c>.
    /// </summary>
    public bool IncludeRequestBody { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to capture and log the HTTP response body. Default is <c>false</c>.
    /// </summary>
    public bool IncludeResponseBody { get; set; }

    /// <summary>
    /// Gets or sets the maximum body length in bytes to read and log. Prevents unbounded memory allocation. Default is 32 KB.
    /// </summary>
    public int MaxBodyLength { get; set; } = 32 * 1024;

    /// <summary>
    /// Gets or sets the HTTP header name used to read/propagate correlation IDs. Default is <c>X-Correlation-ID</c>.
    /// </summary>
    public string CorrelationHeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets the HTTP header name used to read/propagate tenant IDs. Default is <c>X-Tenant-ID</c>.
    /// </summary>
    public string TenantHeaderName { get; set; } = "X-Tenant-ID";

    /// <summary>
    /// Gets or sets a list of path prefixes to exclude from HTTP logging (e.g., "/health", "/metrics", "/swagger").
    /// </summary>
    public List<string> ExcludedPaths { get; set; } =
    [
        "/health",
        "/metrics",
        "/swagger",
        "/favicon.ico"
    ];

    /// <summary>
    /// Gets or sets the log level used when logging successful HTTP requests. Default is <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets a value indicating whether to sanitize/mask sensitive data in headers and body. Default is <c>true</c>.
    /// </summary>
    public bool MaskSensitiveData { get; set; } = true;
}
