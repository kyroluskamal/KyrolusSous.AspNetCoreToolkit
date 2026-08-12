namespace KyrolusSous.Validation.Abstractions;

public sealed record KyrolusValidationMetricsContext(
    Type? RequestType,
    KyrolusValidationContext Context,
    IReadOnlyList<KyrolusValidationFailure> Failures,
    TimeSpan Duration);

public interface IKyrolusValidationMetrics
{
    ValueTask RecordAsync(
        KyrolusValidationMetricsContext context,
        CancellationToken cancellationToken = default);
}
