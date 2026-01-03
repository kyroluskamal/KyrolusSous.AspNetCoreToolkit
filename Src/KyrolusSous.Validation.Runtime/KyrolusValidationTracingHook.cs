using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationTracingHook(IKyrolusValidationTracer tracer) : IKyrolusValidationHook
{
    private readonly IKyrolusValidationTracer tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
    private readonly AsyncLocal<object?> state = new();

    public ValueTask OnBeforeAsync(
        object? request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var traceContext = new KyrolusValidationTraceContext(request?.GetType(), context);
        state.Value = tracer.Start(traceContext);
        return ValueTask.CompletedTask;
    }

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
