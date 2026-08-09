namespace KyrolusSous.Validation.Abstractions;

public interface IKyrolusRequestValidator<in TRequest>
{
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}

public interface IKyrolusRequestValidatorWithContext<in TRequest> : IKyrolusRequestValidator<TRequest>
{
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default);
}
