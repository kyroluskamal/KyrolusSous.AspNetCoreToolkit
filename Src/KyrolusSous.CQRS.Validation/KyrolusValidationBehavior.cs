namespace KyrolusSous.CQRS.Validation;

public sealed class KyrolusValidationBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestValidator<TRequest>> validators)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        List<KyrolusValidationFailure> failures = [];
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Count > 0)
            {
                failures.AddRange(result);
            }
        }

        if (failures.Count > 0)
        {
            throw new KyrolusValidationException(failures);
        }

        return await next();
    }
}
