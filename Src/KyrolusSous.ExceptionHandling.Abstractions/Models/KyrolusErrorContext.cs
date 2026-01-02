namespace KyrolusSous.ExceptionHandling.Abstractions.Models;

public sealed record KyrolusErrorContext(
    string? TraceId,
    string? CorrelationId,
    string? UserId,
    string? TenantId,
    string? Path,
    string? Method,
    CultureInfo? Culture);
