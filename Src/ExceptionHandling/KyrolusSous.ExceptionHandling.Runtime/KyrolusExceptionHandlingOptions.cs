namespace KyrolusSous.ExceptionHandling.Runtime;

/// <summary>
/// Configuration options for the exception handling middleware and runtime pipeline.
/// </summary>
public sealed class KyrolusExceptionHandlingOptions
{
    /// <summary>Gets or sets whether to include detailed exception stack traces in HTTP responses.</summary>
    public bool IncludeExceptionDetailsInResponse { get; set; }

    /// <summary>Gets or sets whether to automatically include exception details when running in the Development environment. Defaults to <c>true</c>.</summary>
    public bool IncludeExceptionDetailsInDevelopment { get; set; } = true;

    /// <summary>Gets or sets whether to include the activity TraceId in error response envelopes. Defaults to <c>true</c>.</summary>
    public bool IncludeTraceId { get; set; } = true;

    /// <summary>Gets or sets whether to extract and return correlation IDs in error responses. Defaults to <c>true</c>.</summary>
    public bool IncludeCorrelationId { get; set; } = true;

    /// <summary>Gets or sets whether to capture ambient request context metadata (path, method, user, tenant). Defaults to <c>true</c>.</summary>
    public bool IncludeContextMetadata { get; set; } = true;

    /// <summary>Gets or sets the HTTP request header name used to look up correlation IDs. Defaults to <c>"X-Correlation-ID"</c>.</summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>Gets or sets the claim type used to identify the current user ID. Defaults to <see cref="ClaimTypes.NameIdentifier"/>.</summary>
    public string? UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>Gets or sets the claim type used to identify the current tenant ID. Defaults to <c>"tenant_id"</c>.</summary>
    public string? TenantIdClaimType { get; set; } = "tenant_id";

    /// <summary>Gets or sets whether to log exceptions that were successfully mapped and handled. Defaults to <c>false</c>.</summary>
    public bool LogHandledExceptions { get; set; }

    /// <summary>Gets or sets whether to log unhandled 500 crashes. Defaults to <c>true</c>.</summary>
    public bool LogUnhandledExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to strictly enforce that all domain error codes must be pre-registered in <see cref="Abstractions.Models.KyrolusErrorCodeRegistry"/>.
    /// </summary>
    public bool EnforceErrorCodeRegistry { get; set; }

    /// <summary>Gets or sets the delegate that chooses the log level for a given exception mapping.</summary>
    public Func<KyrolusExceptionMapping, Exception, LogLevel> LogLevelSelector { get; set; } = (mapping, _) =>
        (int)mapping.StatusCode >= 500 ? LogLevel.Error : LogLevel.Warning;

    /// <summary>Gets the set of exception types that should be excluded from server-side logging.</summary>
    public HashSet<Type> IgnoredExceptionLogTypes { get; } = [];

    /// <summary>Gets or sets whether to sanitize sensitive keys from metadata dictionaries. Defaults to <c>true</c>.</summary>
    public bool SanitizeMetadata { get; set; } = true;

    /// <summary>Gets the set of sensitive keys to scrub from response metadata.</summary>
    public HashSet<string> SensitiveMetadataKeys { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "secret",
        "token",
        "authorization",
        "cookie",
        "set-cookie",
        "api-key",
        "apikey",
        "access_token",
        "refresh_token",
        "jwt"
    };

    /// <summary>Gets or sets an optional allowlist of permitted metadata keys. If set, only keys in this set are preserved.</summary>
    public HashSet<string>? MetadataAllowList { get; set; }

    /// <summary>
    /// Configures the options to suppress logging for noisy exceptions like <see cref="OperationCanceledException"/> and <see cref="BadHttpRequestException"/>.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public KyrolusExceptionHandlingOptions IgnoreCommonNoisyExceptions()
    {
        IgnoredExceptionLogTypes.Add(typeof(OperationCanceledException));
        IgnoredExceptionLogTypes.Add(typeof(TaskCanceledException));
        IgnoredExceptionLogTypes.Add(typeof(BadHttpRequestException));
        return this;
    }

    /// <summary>
    /// Suppresses server logging for a specific exception type.
    /// </summary>
    /// <typeparam name="TException">The exception type to ignore.</typeparam>
    /// <returns>The current options instance for chaining.</returns>
    public KyrolusExceptionHandlingOptions IgnoreLoggingFor<TException>() where TException : Exception
    {
        IgnoredExceptionLogTypes.Add(typeof(TException));
        return this;
    }

    /// <summary>
    /// Suppresses server logging for a specific exception type.
    /// </summary>
    /// <param name="exceptionType">The exception type to ignore.</param>
    /// <returns>The current options instance for chaining.</returns>
    public KyrolusExceptionHandlingOptions IgnoreLoggingFor(Type exceptionType)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);
        IgnoredExceptionLogTypes.Add(exceptionType);
        return this;
    }
}
