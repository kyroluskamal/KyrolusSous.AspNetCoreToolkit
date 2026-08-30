namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents a domain-level business rule violation, integrated with <see cref="KyrolusErrorCodeRegistry"/>.
/// </summary>
/// <remarks>
/// Use this exception when a business invariant or domain logic rule is violated.
/// It dynamically resolves its HTTP status code, title, and logging policy from the central <see cref="KyrolusErrorCodeRegistry"/>
/// or accepts explicit overrides.
/// </remarks>
/// <example>
/// <code>
/// // Use Case 1: Standard domain rule violation (fetches 422 and title from Registry)
/// if (order.IsAlreadyShipped)
///     throw new KyrolusDomainException("order_already_shipped", "Cannot cancel an order that has already shipped.");
/// 
/// // Use Case 2: Transient failure with retry indication
/// if (seat.IsTemporarilyLocked)
///     throw new KyrolusDomainException("seat_locked", "Seat is locked by another checkout session.", null, isTransient: true);
/// 
/// // Use Case 3: Explicit status code bypass
/// throw new KyrolusDomainException(HttpStatusCode.PaymentRequired, "quota_exceeded", "Monthly API Quota Exceeded");
/// </code>
/// </example>
public class KyrolusDomainException : KyrolusException
{
    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusDomainException"/> driven by the <see cref="KyrolusErrorCodeRegistry"/>.
    /// </summary>
    /// <param name="code">The registered domain error code (e.g. "insufficient_funds").</param>
    /// <param name="detail">An optional detailed explanation of the violation.</param>
    /// <param name="errors">An optional collection of individual field validation items.</param>
    /// <param name="metadata">An optional dictionary of key-value diagnostic metadata.</param>
    /// <param name="isTransient">Optional override for whether the failure is retryable. If null, resolves from registry.</param>
    /// <param name="shouldLog">Optional override for whether to log this error. If null, resolves from registry.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public KyrolusDomainException(
        string code,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        bool? isTransient = null,
        bool? shouldLog = null,
        Exception? innerException = null)
        : base(
            ResolveStatusCode(code),
            code,
            ResolveTitle(code),
            detail,
            errors,
            metadata,
            isTransient ?? ResolveIsTransient(code),
            shouldLog ?? ResolveShouldLog(code),
            innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusDomainException"/> indicating a transient/retryable state.
    /// </summary>
    /// <param name="code">The registered domain error code.</param>
    /// <param name="detail">The detailed explanation.</param>
    /// <param name="errors">Optional list of field errors.</param>
    /// <param name="isTransient">Indicates that the failure is temporary and can be retried by the caller.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public KyrolusDomainException(
        string code,
        string? detail,
        IReadOnlyList<KyrolusErrorItem>? errors,
        bool isTransient,
        Exception? innerException = null)
        : this(code, detail, errors, null, isTransient, null, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusDomainException"/> with explicit HTTP status code and title, bypassing the registry.
    /// </summary>
    /// <param name="statusCode">The explicit HTTP status code (e.g., <see cref="HttpStatusCode.UnprocessableEntity"/>).</param>
    /// <param name="code">The unique error code identifier.</param>
    /// <param name="title">The error title.</param>
    /// <param name="detail">The optional detailed explanation.</param>
    /// <param name="errors">Optional list of field errors.</param>
    /// <param name="metadata">Optional dictionary of diagnostic metadata.</param>
    /// <param name="isTransient">Indicates if the error is retryable.</param>
    /// <param name="shouldLog">Indicates if the error should be logged on the server.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public KyrolusDomainException(
        HttpStatusCode statusCode,
        string code,
        string title,
        string? detail = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        bool isTransient = false,
        bool shouldLog = true,
        Exception? innerException = null)
        : base(statusCode, code, title, detail, errors, metadata, isTransient, shouldLog, innerException)
    {
    }

    private static KyrolusErrorCodeDefinition? ResolveDefinition(string code)
    {
        if (KyrolusErrorCodeRegistry.TryGet(code, out var definition))
            return definition;

        if (KyrolusErrorCodeRegistry.StrictMode)
            throw new KyrolusErrorCodeRegistryException(
                $"[Strict Mode Violation] Error code '{code}' is not registered in KyrolusErrorCodeRegistry. " +
                $"Please register the error code during application startup before using it.");

        return null;
    }

    private static HttpStatusCode ResolveStatusCode(string code)
        => ResolveDefinition(code)?.StatusCode ?? HttpStatusCode.BadRequest;

    private static string ResolveTitle(string code)
        => ResolveDefinition(code)?.Title ?? code;

    private static bool ResolveIsTransient(string code)
        => ResolveDefinition(code)?.IsTransient ?? false;

    private static bool ResolveShouldLog(string code)
        => ResolveDefinition(code)?.ShouldLog ?? true;
}
