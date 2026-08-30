namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

/// <summary>
/// Represents the standard structured error envelope model serialized into HTTP response payloads.
/// </summary>
/// <param name="Code">The machine-readable error code string.</param>
/// <param name="Title">The short summary title of the error.</param>
/// <param name="Detail">The optional detailed explanation.</param>
/// <param name="TraceId">The tracing trace identifier for log correlation.</param>
/// <param name="Errors">Optional collection of field-level errors.</param>
/// <param name="Metadata">Optional dictionary of diagnostic metadata.</param>
public sealed record KyrolusErrorEnvelope(
    string Code,
    string Title,
    string? Detail = null,
    string? TraceId = null,
    IReadOnlyList<KyrolusErrorItem>? Errors = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
