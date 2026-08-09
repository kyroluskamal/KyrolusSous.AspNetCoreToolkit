namespace KyrolusSous.CQRS.Validation;

public sealed class KyrolusValidationBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestValidator<TRequest>> validators,
    IKyrolusValidationEngine? engine = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<KyrolusValidationFailure> failures;
        if (engine is not null)
        {
            failures = await engine.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        else if (!validators.Any())
        {
            return await next();
        }
        else
        {
            List<KyrolusValidationFailure> collected = [];
            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
                if (result.Count > 0)
                {
                    collected.AddRange(result);
                }
            }
            failures = collected;
        }

        if (failures.Count > 0)
        {
            throw new KyrolusValidationException(failures);
        }

        return await next();
    }
}
