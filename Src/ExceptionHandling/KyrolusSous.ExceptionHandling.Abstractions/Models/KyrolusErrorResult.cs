namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

/// <summary>
/// Represents the resolved result of translating an exception, containing the error envelope, status code, and transient flag.
/// </summary>
/// <param name="Error">The structured error envelope.</param>
/// <param name="StatusCode">The mapped HTTP status code.</param>
/// <param name="IsTransient">Indicates if the failure is temporary.</param>
/// <param name="ExceptionType">The CLR type name of the original exception.</param>
public sealed record KyrolusErrorResult(
    KyrolusErrorEnvelope Error,
    HttpStatusCode? StatusCode = null,
    bool IsTransient = false,
    string? ExceptionType = null);
