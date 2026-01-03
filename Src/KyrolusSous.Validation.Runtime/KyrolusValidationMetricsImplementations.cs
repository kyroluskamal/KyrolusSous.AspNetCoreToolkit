using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusNoopValidationMetrics : IKyrolusValidationMetrics
{
    public static readonly IKyrolusValidationMetrics Instance = new KyrolusNoopValidationMetrics();

    public ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class KyrolusDelegateValidationMetrics(Func<KyrolusValidationMetricsContext, CancellationToken, ValueTask> execute)
    : IKyrolusValidationMetrics
{
    private readonly Func<KyrolusValidationMetricsContext, CancellationToken, ValueTask> execute = execute
        ?? throw new ArgumentNullException(nameof(execute));

    public ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default)
    {
        return execute(context, cancellationToken);
    }
}
