using System.ComponentModel.DataAnnotations;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.Validation.DataAnnotations;

public sealed class DataAnnotationsRequestValidator<TRequest> : IKyrolusRequestValidatorWithContext<TRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request, CancellationToken cancellationToken = default) => ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken);

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        TRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(
                [new KyrolusValidationFailure(string.Empty, "Request is required.")]);

        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(request, serviceProvider: null, items: null);
        if (context is not null)
            validationContext.Items[nameof(KyrolusValidationContext)] = context;

        var isValid = Validator.TryValidateObject(request, validationContext, results, validateAllProperties: true);
        
        if (isValid)
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(Array.Empty<KyrolusValidationFailure>());

        var failures = results
            .SelectMany(result =>
            {
                var error = result.ErrorMessage ?? "Validation error.";
                if (result.MemberNames is null || !result.MemberNames.Any())
                    return [new KyrolusValidationFailure(string.Empty, error)];

                return result.MemberNames.Select(member => new KyrolusValidationFailure(member, error));
            })
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(failures);
    }
}
