namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

/// <summary>
/// Encapsulates the output of an <see cref="IKyrolusExceptionMapper"/>, pairing an error envelope with its HTTP status code.
/// </summary>
/// <param name="Error">The populated error envelope.</param>
/// <param name="StatusCode">The assigned HTTP status code.</param>
/// <param name="IsTransient">Indicates if the failure is temporary.</param>
/// <param name="ShouldLog">Indicates if the exception should be logged on the server.</param>
public sealed record KyrolusExceptionMapping(
    KyrolusErrorEnvelope Error,
    HttpStatusCode StatusCode,
    bool IsTransient = false,
    bool ShouldLog = true)
{
    /// <summary>
    /// Creates a clone with modified transient flag.
    /// </summary>
    /// <param name="isTransient">Indicates if the failure is temporary.</param>
    /// <returns>A modified copy of the mapping.</returns>
    public KyrolusExceptionMapping AsTransient(bool isTransient = true) => this with { IsTransient = isTransient };

    /// <summary>
    /// Creates a clone with modified logging policy.
    /// </summary>
    /// <param name="shouldLog"><c>true</c> to log; otherwise, <c>false</c>.</param>
    /// <returns>A modified copy of the mapping.</returns>
    public KyrolusExceptionMapping WithLogging(bool shouldLog) => this with { ShouldLog = shouldLog };

    /// <summary>
    /// Creates a clone with logging disabled.
    /// </summary>
    /// <returns>A modified copy of the mapping.</returns>
    public KyrolusExceptionMapping WithoutLogging() => this with { ShouldLog = false };

    /// <summary>
    /// Factory helper to build a new <see cref="KyrolusExceptionMapping"/> instance conveniently.
    /// </summary>
    /// <param name="code">The unique error code.</param>
    /// <param name="title">The short title summary.</param>
    /// <param name="statusCode">The target HTTP status code.</param>
    /// <param name="detail">The optional detailed explanation.</param>
    /// <param name="traceId">The optional trace ID.</param>
    /// <param name="errors">Optional list of field-level errors.</param>
    /// <param name="metadata">Optional dictionary of metadata.</param>
    /// <returns>A new <see cref="KyrolusExceptionMapping"/> instance.</returns>
    public static KyrolusExceptionMapping Create(
        string code,
        string title,
        HttpStatusCode statusCode,
        string? detail = null,
        string? traceId = null,
        IReadOnlyList<KyrolusErrorItem>? errors = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        => new(
            new KyrolusErrorEnvelope(code, title, detail, traceId, errors, metadata),
            statusCode);
}
