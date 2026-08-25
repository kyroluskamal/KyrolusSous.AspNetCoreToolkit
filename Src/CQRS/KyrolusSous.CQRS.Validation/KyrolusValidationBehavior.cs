using KyrolusSous.Mediator.Abstractions;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.CQRS.Validation;

[PipelineOrder(-500)]
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
