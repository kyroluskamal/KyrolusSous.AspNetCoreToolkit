using FluentValidation;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.FluentValidation;

public sealed class FluentValidationRequestValidator<TRequest>(IValidator<TRequest> validator)
    : IKyrolusRequestValidator<TRequest>
{
    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
        {
            return Array.Empty<KyrolusValidationFailure>();
        }

        var failures = result.Errors
            .Where(error => error is not null)
            .Select(error => new KyrolusValidationFailure(error.PropertyName, error.ErrorMessage))
            .ToArray();

        return failures;
    }
}
