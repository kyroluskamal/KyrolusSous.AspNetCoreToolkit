using KyrolusSous.Mediator.Abstractions;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Validation.Abstractions;

namespace KyrolusSous.CQRS.Validation;

// Runs after Audit(-2050), Authorization(-1050) and TenantScoping(-1040) - so Audit still wraps and
// records a validation rejection on an auditable command - and before PropertyAllowList(-940),
// Performance(-900), Idempotency(-800) and Throttling(-750), so a malformed request is rejected
// before it consumes an idempotency slot or is throttle-counted as if it were a legitimate attempt.
[PipelineOrder(-950)]
public sealed class KyrolusValidationBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestValidator<TRequest>>? validators = null,
    IKyrolusValidationEngine? engine = null,
    KyrolusValidationBehaviorOptions? options = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusRequestValidator<TRequest>> _validators =
        validators as IReadOnlyList<IKyrolusRequestValidator<TRequest>> ?? (validators is not null ? [.. validators] : []);
    private readonly IKyrolusValidationEngine? _engine = engine;
    private readonly KyrolusValidationSeverity _minimumBlockingSeverity = options?.MinimumBlockingSeverity ?? KyrolusValidationSeverity.Error;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        if (_engine is null && _validators.Count == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        // Additive, not either/or: every other behavior in this pipeline composes multiple sources
        // the same way (multiple audit sinks, multiple exception actions, ...), and an engine
        // registered app-wide alongside a targeted IKyrolusRequestValidator<TRequest> for one
        // command must not make that extra check silently stop running - which is exactly what
        // happened here before, since the engine branch used to run INSTEAD of the validators
        // branch rather than alongside it.
        List<KyrolusValidationFailure> collected = [];

        // Opt-in per-request scoping (RuleSets/Groups/Profiles/MinimumSeverity) via
        // IKyrolusValidationContextRequest - see that interface's remarks. A request that does not
        // implement it, or returns a null context, falls straight through to the context-free
        // overloads below exactly as before this feature existed.
        var validationContext = (request as IKyrolusValidationContextRequest)?.ValidationContext;

        if (_engine is not null)
        {
            var engineResult = validationContext is not null
                ? await _engine.ValidateAsync(request, validationContext, cancellationToken).ConfigureAwait(false)
                : await _engine.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (engineResult is not null && engineResult.Count > 0)
            {
                collected.AddRange(engineResult);
            }
        }

        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = validationContext is not null && validator is IKyrolusRequestValidatorWithContext<TRequest> contextValidator
                ? await contextValidator.ValidateAsync(request, validationContext, cancellationToken).ConfigureAwait(false)
                : await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (result is not null && result.Count > 0)
            {
                collected.AddRange(result);
            }
        }

        // Info/Warning failures are documented as non-blocking hints (see KyrolusValidationSeverity);
        // only failures at or above the configured threshold should actually reject the request.
        var blocking = collected.Count == 0
            ? collected
            : collected.Where(f => f.Severity >= _minimumBlockingSeverity).ToList();

        if (blocking.Count > 0)
        {
            throw new KyrolusValidationException(blocking);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
