namespace KyrolusSous.Validation.Abstractions;

public interface IKyrolusValidationEngine
{
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync<TRequest>(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond>(
        TFirst first,
        TSecond second,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird, TFourth>(
        TFirst first,
        TSecond second,
        TThird third,
        TFourth fourth,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateCompositeAsync<TFirst, TSecond, TThird, TFourth>(
        TFirst first,
        TSecond second,
        TThird third,
        TFourth fourth,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);
}
