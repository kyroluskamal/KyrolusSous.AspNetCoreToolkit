using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.Fluent;

public interface IValidationRule<in T>
{
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(T request, CancellationToken cancellationToken = default);
}
