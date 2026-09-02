using System.Diagnostics.Metrics;

namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// The default <see cref="IKyrolusValidationMetrics"/>: discards every recorded run. Registered by
/// <see cref="ServiceCollectionExtensions.AddKyrolusValidationRuntime"/> via <c>TryAddSingleton</c>, so
/// registering a real implementation before that call replaces it automatically.
/// </summary>
public sealed class KyrolusNoopValidationMetrics : IKyrolusValidationMetrics
{
    /// <summary>A shared, reusable instance, since this implementation has no state.</summary>
    public static readonly IKyrolusValidationMetrics Instance = new KyrolusNoopValidationMetrics();

    /// <inheritdoc />
    public ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default)
    => ValueTask.CompletedTask;
}

/// <summary>
/// <see cref="IKyrolusValidationMetrics"/> that forwards each recorded run to a supplied delegate - a shortcut
/// for wiring up simple metrics (e.g. a single counter increment or log line) without writing a dedicated class.
/// </summary>
/// <param name="execute">The delegate invoked with each run's <see cref="KyrolusValidationMetricsContext"/>.</param>
/// <example>
/// <code>
/// services.AddSingleton&lt;IKyrolusValidationMetrics&gt;(new KyrolusDelegateValidationMetrics((ctx, ct) =&gt;
/// {
///     logger.LogInformation("Validated {Type} in {Ms}ms, {Count} failures", ctx.RequestType, ctx.Duration.TotalMilliseconds, ctx.Failures.Count);
///     return ValueTask.CompletedTask;
/// }));
/// </code>
/// </example>
public sealed class KyrolusDelegateValidationMetrics(Func<KyrolusValidationMetricsContext, CancellationToken, ValueTask> execute)
    : IKyrolusValidationMetrics
{
    private readonly Func<KyrolusValidationMetricsContext, CancellationToken, ValueTask> execute = execute
        ?? throw new ArgumentNullException(nameof(execute));

    /// <inheritdoc />
    public ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default)
        => execute(context, cancellationToken);
}

/// <summary>
/// <see cref="IKyrolusValidationMetrics"/> backed by <see cref="System.Diagnostics.Metrics.Meter"/> - the
/// standard .NET metrics API. Any OpenTelemetry-configured app (or any other <c>System.Diagnostics.Metrics</c>
/// listener) picks up its instruments automatically by listening for <see cref="Meter"/>'s name, without this
/// package needing a direct reference to any OpenTelemetry package - mirroring how
/// <see cref="KyrolusValidationActivityTracer"/> does the same for tracing. Not registered by default; opt in
/// explicitly (the default is <see cref="KyrolusNoopValidationMetrics"/>).
/// </summary>
/// <remarks>
/// Publishes three instruments, each tagged with <c>validation.request_type</c> (the validated type's simple
/// name) and <c>validation.outcome</c> (<c>"passed"</c> or <c>"failed"</c>):
/// <list type="bullet">
/// <item><description><c>kyrolus.validation.executions</c> (<see cref="Counter{T}">Counter&lt;long&gt;</see>) - incremented once per recorded run.</description></item>
/// <item><description><c>kyrolus.validation.duration</c> (<see cref="Histogram{T}">Histogram&lt;double&gt;</see>, milliseconds) - the run's end-to-end elapsed time.</description></item>
/// <item><description><c>kyrolus.validation.failures</c> (<see cref="Counter{T}">Counter&lt;long&gt;</see>) - the number of <see cref="KyrolusValidationFailure"/> items produced; only recorded for a failing run, additionally tagged with <c>validation.max_severity</c>.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs
/// builder.Services.AddSingleton&lt;IKyrolusValidationMetrics&gt;(new KyrolusValidationSystemMetrics());
/// builder.Services.AddKyrolusValidationRuntime();
///
/// // Wherever OpenTelemetry is configured:
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(metrics =&gt; metrics.AddMeter("Kyrolus.Validation"));
/// </code>
/// </example>
public sealed class KyrolusValidationSystemMetrics : IKyrolusValidationMetrics, IDisposable
{
    private const string UnknownRequestType = "Unknown";

    private readonly Meter meter;
    private readonly Counter<long> executionCount;
    private readonly Counter<long> failureCount;
    private readonly Histogram<double> duration;

    /// <param name="meterName">The <see cref="Meter"/> name OpenTelemetry (or another listener) should subscribe to. Defaults to <c>"Kyrolus.Validation"</c>.</param>
    /// <param name="meterVersion">Optional version tag for the meter, surfaced to consumers that report an instrumentation-scope version (e.g. OpenTelemetry).</param>
    public KyrolusValidationSystemMetrics(string meterName = "Kyrolus.Validation", string? meterVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meterName);

        meter = new Meter(meterName, meterVersion);
        executionCount = meter.CreateCounter<long>(
            "kyrolus.validation.executions",
            unit: "{run}",
            description: "Number of validation runs completed, tagged by request type and pass/fail outcome.");
        duration = meter.CreateHistogram<double>(
            "kyrolus.validation.duration",
            unit: "ms",
            description: "Time taken to complete a validation run, end to end.");
        failureCount = meter.CreateCounter<long>(
            "kyrolus.validation.failures",
            unit: "{failure}",
            description: "Number of individual validation failures produced by failing runs.");
    }

    /// <inheritdoc />
    public ValueTask RecordAsync(KyrolusValidationMetricsContext context, CancellationToken cancellationToken = default)
    {
        var requestType = context.RequestType?.Name ?? UnknownRequestType;
        var passed = context.Failures.Count == 0;
        var outcome = passed ? "passed" : "failed";

        executionCount.Add(1,
            new KeyValuePair<string, object?>("validation.request_type", requestType),
            new KeyValuePair<string, object?>("validation.outcome", outcome));

        duration.Record(context.Duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("validation.request_type", requestType),
            new KeyValuePair<string, object?>("validation.outcome", outcome));

        if (!passed)
        {
            var maxSeverity = context.Failures.Max(f => f.Severity);
            failureCount.Add(context.Failures.Count,
                new KeyValuePair<string, object?>("validation.request_type", requestType),
                new KeyValuePair<string, object?>("validation.max_severity", maxSeverity.ToString()));
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Disposes the underlying <see cref="Meter"/>, unpublishing its instruments. When this instance is
    /// registered via <c>AddSingleton</c>, the DI container calls this automatically on shutdown; dispose it
    /// yourself only if you constructed it outside DI.
    /// </summary>
    public void Dispose() => meter.Dispose();
}
