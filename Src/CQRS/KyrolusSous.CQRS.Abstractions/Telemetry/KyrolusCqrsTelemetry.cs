using System.Diagnostics;

namespace KyrolusSous.CQRS.Abstractions.Telemetry;

/// <summary>
/// OpenTelemetry ActivitySource and Metrics instrumentation constants for Kyrolus CQRS.
/// </summary>
public static class KyrolusCqrsTelemetry
{
    public const string ActivitySourceName = "KyrolusSous.CQRS";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    public const string TagRequestType = "cqrs.request.type";
    public const string TagRequestKind = "cqrs.request.kind";
    public const string TagExecutionDurationMs = "cqrs.execution.duration_ms";
    public const string TagSlowRequest = "cqrs.is_slow";
    public const string TagIdempotencyKey = "cqrs.idempotency_key";
    public const string TagIdempotencyHit = "cqrs.idempotency_hit";
}
