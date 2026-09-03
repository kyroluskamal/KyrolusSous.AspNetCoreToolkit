using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KyrolusSous.CQRS.Abstractions.Telemetry;

/// <summary>
/// OpenTelemetry ActivitySource and Metrics instrumentation constants for Kyrolus CQRS.
/// </summary>
public static class KyrolusCqrsTelemetry
{
    public const string ActivitySourceName = "KyrolusSous.CQRS";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    public const string MeterName = "KyrolusSous.CQRS";
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public const string TagRequestType = "cqrs.request.type";
    public const string TagRequestKind = "cqrs.request.kind";
    public const string TagExecutionDurationMs = "cqrs.execution.duration_ms";
    public const string TagSlowRequest = "cqrs.is_slow";
    public const string TagIdempotencyKey = "cqrs.idempotency_key";
    public const string TagIdempotencyHit = "cqrs.idempotency_hit";

    public const string TagOutboxEventType = "cqrs.outbox.event_type";
    public const string TagOutboxOutcome = "cqrs.outbox.outcome";

    /// <summary>Incremented once per outbox message the processor attempts, tagged by event type and outcome ("processed" or "failed").</summary>
    public static readonly Counter<long> OutboxMessagesProcessed = Meter.CreateCounter<long>(
        "kyrolus.cqrs.outbox.messages",
        unit: "{message}",
        description: "Number of outbox messages processed, tagged by event type and outcome.");
}
