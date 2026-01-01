namespace KyrolusSous.Validation.Abstractions;

public interface IKyrolusRequestValidator<in TRequest>
{
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}
