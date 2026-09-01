namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// Always-registered <see cref="IKyrolusValidationHook"/> that opens and closes a tracing span around every
/// validation call via the registered <see cref="IKyrolusValidationTracer"/> - a no-op
/// (<see cref="KyrolusNoopValidationTracer"/>) by default. Register <see cref="KyrolusValidationActivityTracer"/>
/// (or your own <see cref="IKyrolusValidationTracer"/>) to start emitting spans, without touching this class.
/// </summary>
/// <param name="tracer">The tracer to start/stop a span through for each run.</param>
public sealed class KyrolusValidationTracingHook(IKyrolusValidationTracer tracer) : IKyrolusValidationHook
{
    private readonly IKyrolusValidationTracer tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));

    /// <summary>
    /// Per-call span state (the value <see cref="IKyrolusValidationTracer.Start"/> returned).
    /// <see cref="AsyncLocal{T}"/> because this hook is a singleton shared across concurrent calls, so each
    /// logical call needs its own state flowing with its async context.
    /// </summary>
    private readonly AsyncLocal<object?> state = new();

    /// <inheritdoc />
    public ValueTask OnBeforeAsync(
        object? request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var traceContext = new KyrolusValidationTraceContext(request?.GetType(), context);
        state.Value = tracer.Start(traceContext);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnAfterAsync(
        object? request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default)
    {
        var traceContext = new KyrolusValidationTraceContext(request?.GetType(), context);
        var current = state.Value;
        state.Value = null;
        return tracer.StopAsync(traceContext, current, failures, null, cancellationToken);
    }
}
