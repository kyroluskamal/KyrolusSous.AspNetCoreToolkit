namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// The default <see cref="IKyrolusValidationTracer"/>: starts no span and records nothing. Registered by
/// <see cref="ServiceCollectionExtensions.AddKyrolusValidationRuntime"/> via <c>TryAddSingleton</c>, so
/// registering <see cref="KyrolusValidationActivityTracer"/> (or your own tracer) before that call replaces it.
/// </summary>
public sealed class KyrolusNoopValidationTracer : IKyrolusValidationTracer
{
    /// <summary>A shared, reusable instance, since this implementation has no state.</summary>
    public static readonly IKyrolusValidationTracer Instance = new KyrolusNoopValidationTracer();

    /// <inheritdoc />
    public object? Start(KyrolusValidationTraceContext context) => null;

    /// <inheritdoc />
    public ValueTask StopAsync(
        KyrolusValidationTraceContext context,
        object? state,
        IReadOnlyList<KyrolusValidationFailure> failures,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

/// <summary>
/// <see cref="IKyrolusValidationTracer"/> backed by <see cref="ActivitySource"/> - the
/// standard .NET tracing primitive. Any OpenTelemetry-configured app picks up its spans automatically by
/// listening for <paramref name="sourceName"/>, without this package needing a direct reference to any
/// OpenTelemetry package. Not registered by default; opt in explicitly (see <see cref="KyrolusNoopValidationTracer"/>).
/// </summary>
/// <param name="sourceName">The <see cref="ActivitySource"/> name OpenTelemetry (or another listener) should subscribe to.</param>
public sealed class KyrolusValidationActivityTracer(string sourceName = "Kyrolus.Validation")
    : IKyrolusValidationTracer
{
    private readonly ActivitySource source = new(sourceName);

    /// <inheritdoc />
    public object? Start(KyrolusValidationTraceContext context)
    {
        var activity = source.StartActivity("Validation", ActivityKind.Internal);
        if (activity is null) return null;

        activity.SetTag("validation.request_type", context.RequestType?.FullName);
        activity.SetTag("validation.rule_sets", context.Context.RuleSets is { Count: > 0 }
            ? string.Join(",", context.Context.RuleSets)
            : null);
        activity.SetTag("validation.groups", context.Context.Groups is { Count: > 0 }
            ? string.Join(",", context.Context.Groups)
            : null);
        activity.SetTag("validation.min_severity", context.Context.MinimumSeverity?.ToString());

        return activity;
    }

    /// <inheritdoc />
    public ValueTask StopAsync(
        KyrolusValidationTraceContext context,
        object? state,
        IReadOnlyList<KyrolusValidationFailure> failures,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        if (state is Activity activity)
        {
            activity.SetTag("validation.failures", failures.Count);
            if (failures.Count > 0)
            {
                var maxSeverity = failures.Max(f => f.Severity);
                activity.SetTag("validation.max_severity", maxSeverity.ToString());
            }

            if (exception is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.SetTag("validation.exception", exception.GetType().FullName);
            }

            activity.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
