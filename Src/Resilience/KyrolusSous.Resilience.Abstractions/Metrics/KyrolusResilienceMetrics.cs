using System.Diagnostics.Metrics;

namespace KyrolusSous.Resilience;

/// <summary>
/// OpenTelemetry and Prometheus instrumentation metrics for Kyrolus resilience pipelines.
/// </summary>
public static class KyrolusResilienceMetrics
{
    public const string MeterName = "KyrolusSous.Resilience";
    public const string MeterVersion = "1.0.0";

    private static readonly Meter Meter = new(MeterName, MeterVersion);

    public static readonly Counter<long> ExecutionsTotal = Meter.CreateCounter<long>(
        "resilience.executions.total",
        description: "Total number of resilience pipeline executions.");

    public static readonly Counter<long> RetriesTotal = Meter.CreateCounter<long>(
        "resilience.retries.total",
        description: "Total number of retry attempts triggered across all pipelines.");

    public static readonly Counter<long> HedgedAttemptsTotal = Meter.CreateCounter<long>(
        "resilience.hedged.total",
        description: "Total number of speculative hedged execution attempts.");

    public static readonly Histogram<double> ExecutionDurationMs = Meter.CreateHistogram<double>(
        "resilience.execution.duration.ms",
        unit: "ms",
        description: "Duration of resilience pipeline executions in milliseconds.");
}
