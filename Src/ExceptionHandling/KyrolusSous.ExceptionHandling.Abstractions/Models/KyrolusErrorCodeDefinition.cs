namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

/// <summary>
/// Defines the registration metadata for a specific domain error code in <see cref="KyrolusErrorCodeRegistry"/>.
/// </summary>
/// <param name="Code">The unique machine-readable error code string (e.g. "insufficient_funds", "user_suspended").</param>
/// <param name="Title">The standard human-readable summary title for this error type.</param>
/// <param name="StatusCode">The default HTTP status code associated with this error (e.g. <see cref="HttpStatusCode.UnprocessableEntity"/>).</param>
/// <param name="Description">An optional documentation description explaining when this error code occurs.</param>
/// <param name="IsTransient">Indicates if the error represents a temporary/retryable failure.</param>
/// <param name="ShouldLog">Indicates if this error should be logged on the server. Defaults to <c>true</c>.</param>
public sealed record KyrolusErrorCodeDefinition(
    string Code,
    string Title,
    HttpStatusCode StatusCode,
    string? Description = null,
    bool IsTransient = false,
    bool ShouldLog = true);
