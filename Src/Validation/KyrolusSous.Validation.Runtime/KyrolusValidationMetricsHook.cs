
namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationMetricsHook(IKyrolusValidationMetrics metrics) : IKyrolusValidationHook
{
    private readonly IKyrolusValidationMetrics metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    private readonly AsyncLocal<Stopwatch?> stopwatch = new();

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
