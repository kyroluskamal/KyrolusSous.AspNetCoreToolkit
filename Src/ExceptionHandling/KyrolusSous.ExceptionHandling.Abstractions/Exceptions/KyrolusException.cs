namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents the base abstract class for all domain, business, and HTTP-aware exceptions in the toolkit.
/// </summary>
/// <remarks>
/// Inherit from this class when creating domain-specific exceptions that need to be automatically translated
/// into RFC 7807 ProblemDetails HTTP responses without cluttering controllers with manual <c>try-catch</c> blocks.
/// </remarks>
/// <example>
/// <code>
/// public class InsufficientBalanceException : KyrolusException
/// {
///     public InsufficientBalanceException(decimal currentBalance, decimal requiredAmount)
///         : base(HttpStatusCode.UnprocessableEntity, "insufficient_balance", "Insufficient Balance",
///                $"Current balance {currentBalance:C} is less than required {requiredAmount:C}")
///     {
///         WithMetadata("currentBalance", currentBalance);
///         WithMetadata("requiredAmount", requiredAmount);
///     }
/// }
/// </code>
/// </example>
public abstract class KyrolusException : Exception, IKyrolusExceptionWithErrors, IKyrolusExceptionWithMetadata
{
    private Dictionary<string, object?>? _metadata;
    private List<KyrolusErrorItem>? _errors;

    /// <summary>
    /// Gets the HTTP status code associated with this error (e.g., 400 Bad Request, 404 Not Found, 409 Conflict).
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the unique machine-readable error code string (e.g., "not_found", "insufficient_funds").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets a short, human-readable summary of the problem type (e.g., "Resource Not Found", "Validation Failed").
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets or sets a detailed, human-readable explanation specific to this occurrence of the problem.
    /// </summary>
    public string? Detail { get; protected set; }

    /// <summary>
    /// Gets an optional list of specific validation/field error items associated with this exception.
    /// </summary>
    public IReadOnlyList<KyrolusErrorItem>? Errors => _errors;

    /// <inheritdoc />
    public IReadOnlyList<KyrolusErrorItem>? GetErrors() => _errors;

    /// <summary>
    /// Gets additional key-value diagnostic metadata associated with the exception (e.g., entity IDs, attempted values).
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata => _metadata;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> GetMetadata() => _metadata ?? new Dictionary<string, object?>();

    /// <summary>
    /// Gets a value indicating whether this exception represents a transient/temporary failure that the client can retry.
    /// </summary>
    public bool IsTransient { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether this exception should be written to the server's logging infrastructure.
    /// Set to <c>false</c> for routine business validation errors to prevent log spam.
    /// </summary>
    public bool ShouldLog { get; protected set; }

    /// <summary>
    /// Gets the suggested delay before the client should retry, surfaced as the <c>Retry-After</c> HTTP response
    /// header (see <see cref="KyrolusRateLimitException"/>). <see langword="null"/> when not applicable.
    /// </summary>
    public TimeSpan? RetryAfter { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The unique error code identifier.</param>
    /// <param name="title">The short title describing the error type.</param>
    /// <param name="detail">An optional detailed explanation of the error.</param>
    /// <param name="errors">An optional collection of individual field errors.</param>
    /// <param name="metadata">An optional dictionary of key-value diagnostic metadata.</param>
    /// <param name="isTransient">Indicates if the failure is temporary and safe to retry.</param>
    /// <param name="shouldLog">Indicates if this exception should be logged on the server.</param>
    /// <param name="innerException">An optional inner exception that caused this exception.</param>
    protected KyrolusException(
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        bool isTransient = false,
        bool shouldLog = true,
        Exception? innerException = null) : base(detail ?? title, innerException)
    {
        StatusCode = statusCode;
        Code = code;
        Title = title;
        Detail = detail;
        IsTransient = isTransient;
        ShouldLog = shouldLog;

        if (errors is { Count: > 0 }) _errors = [.. errors];

        if (metadata is { Count: > 0 })
            _metadata = new Dictionary<string, object?>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds or updates a single key-value metadata entry for diagnostic and response enrichment.
    /// </summary>
    /// <param name="key">The metadata key name.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithMetadata(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Adds or merges multiple key-value metadata entries.
    /// </summary>
    /// <param name="metadata">The dictionary of metadata to merge.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        _metadata ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in metadata)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(k); 
            _metadata[k] = v;
        }
        return this;
    }

    /// <summary>
    /// Appends an individual field-level error item to the exception.
    /// </summary>
    /// <param name="field">The name of the invalid property or field.</param>
    /// <param name="message">The specific validation error message.</param>
    /// <param name="code">An optional specific error subcode for the field.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithError(string field, string message, string? code = null)
    {
        _errors ??= [];
        _errors.Add(new KyrolusErrorItem(field, code, message));
        return this;
    }
    /// <summary>
    /// Appends a collection of field-level validation error items to the exception.
    /// </summary>
    /// <param name="errorItems">The collection of error items to append.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithErrors(IEnumerable<KyrolusErrorItem> errorItems)
    {
        ArgumentNullException.ThrowIfNull(errorItems);

        _errors ??= [];
        _errors.AddRange(errorItems);
        return this;
    }

    /// <summary>
    /// Disables server-side logging for this specific exception occurrence.
    /// </summary>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithoutLogging()
    {
        ShouldLog = false;
        return this;
    }

    /// <summary>
    /// Explicitly enables or disables server-side logging for this exception occurrence.
    /// </summary>
    /// <param name="shouldLog"><c>true</c> to log; otherwise, <c>false</c>.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithLogging(bool shouldLog = true)
    {
        ShouldLog = shouldLog;
        return this;
    }

    /// <summary>
    /// Marks this exception as transient (temporary/retryable) or non-transient.
    /// </summary>
    /// <param name="isTransient"><c>true</c> if the client can retry; otherwise, <c>false</c>.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException AsTransient(bool isTransient = true)
    {
        IsTransient = isTransient;
        return this;
    }

    /// <summary>
    /// Updates the detailed explanation for this exception.
    /// </summary>
    /// <param name="detail">The detailed explanation string.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithDetail(string detail)
    {
        Detail = detail;
        return this;
    }

    /// <summary>
    /// Sets the suggested retry delay, surfaced as the <c>Retry-After</c> HTTP response header.
    /// </summary>
    /// <param name="retryAfter">The suggested delay before the client should retry.</param>
    /// <returns>The current exception instance for fluent chaining.</returns>
    public KyrolusException WithRetryAfter(TimeSpan retryAfter)
    {
        RetryAfter = retryAfter;
        return this;
    }
}
