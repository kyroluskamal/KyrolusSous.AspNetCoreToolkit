using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public interface IKyrolusValidationRule<in T>
{
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(T request, CancellationToken cancellationToken = default);
}
