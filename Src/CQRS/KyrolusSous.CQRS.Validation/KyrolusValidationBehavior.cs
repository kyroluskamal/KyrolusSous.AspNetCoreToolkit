using KyrolusSous.Mediator.Abstractions;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.CQRS.Validation;

// Runs right after Authorization/PreProcessor (-1000) and before Performance(-900), Audit(-850),
// Idempotency(-800) and Throttling(-750), so a malformed request is rejected before it is audited,
// consumes an idempotency slot, or is throttle-counted as if it were a legitimate attempt.
[PipelineOrder(-950)]
public sealed class KyrolusValidationBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestValidator<TRequest>>? validators = null,
    IKyrolusValidationEngine? engine = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusRequestValidator<TRequest>> _validators =
        validators as IReadOnlyList<IKyrolusRequestValidator<TRequest>> ?? (validators is not null ? [.. validators] : []);
    private readonly IKyrolusValidationEngine? _engine = engine;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<KyrolusValidationFailure> failures;
        if (_engine is not null)
        {
            failures = await _engine.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        else if (_validators.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            List<KyrolusValidationFailure> collected = [];
            foreach (var validator in _validators)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
                if (result is not null && result.Count > 0)
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

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
