
namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// Always-registered <see cref="IKyrolusValidationHook"/> that times every validation call and reports it to the
/// registered <see cref="IKyrolusValidationMetrics"/> - a no-op (<see cref="KyrolusNoopValidationMetrics"/>) by
/// default. This hook itself never changes: register a real <see cref="IKyrolusValidationMetrics"/> implementation
/// to start recording, without touching this class or the engine.
/// </summary>
/// <param name="metrics">The metrics sink to report each run's <see cref="KyrolusValidationMetricsContext"/> to.</param>
public sealed class KyrolusValidationMetricsHook(IKyrolusValidationMetrics metrics) : IKyrolusValidationHook
{
    private readonly IKyrolusValidationMetrics metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

    /// <summary>
    /// Per-call timer state. <see cref="AsyncLocal{T}"/> (rather than an instance field) because this hook is a
    /// singleton shared across every concurrent <c>ValidateAsync</c> call, so each logical call needs its own
    /// stopwatch that flows with its async context instead of being overwritten by a concurrent call.
    /// </summary>
    private readonly AsyncLocal<Stopwatch?> stopwatch = new();

    /// <inheritdoc />
    public ValueTask OnBeforeAsync(
        object? request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = new Stopwatch();
        sw.Start();
        stopwatch.Value = sw;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnAfterAsync(
        object? request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default)
    {
        var sw = stopwatch.Value;
        if (sw is not null)
        {
            sw.Stop();
            stopwatch.Value = null;
        }

        var metricsContext = new KyrolusValidationMetricsContext(
            request?.GetType(),
            context,
            failures,
            sw?.Elapsed ?? TimeSpan.Zero);

        return metrics.RecordAsync(metricsContext, cancellationToken);
    }
}
