namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public sealed record KyrolusErrorEnvelope(
    string Code,
    string Title,
    string? Detail = null,
    string? TraceId = null,
    IReadOnlyList<KyrolusErrorItem>? Errors = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
