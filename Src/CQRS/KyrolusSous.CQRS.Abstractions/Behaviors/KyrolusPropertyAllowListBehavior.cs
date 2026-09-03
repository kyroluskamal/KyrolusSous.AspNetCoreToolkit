namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior rejecting a property-update request that names a property outside its own
/// declared allow-list.
/// </summary>
/// <remarks>
/// Opt-in via <see cref="IKyrolusPropertyUpdateRequest"/>: a request that does not implement it, or
/// whose <see cref="IKyrolusPropertyUpdateRequest.AllowedProperties"/> is <see langword="null"/>, is
/// untouched by this behavior - existing Patch/BulkPatch/ExecuteUpdate callers keep writing whatever
/// property names they always could. Ordered alongside <c>KyrolusValidationBehavior</c> (-950): a
/// disallowed property name is bad input, not a business-rule failure, so it should be rejected
/// before idempotency claims a slot, audit logs an attempt, or a transaction opens - the same
/// reasoning <c>KyrolusValidationBehavior</c> documents for its own ordering.
/// </remarks>
[PipelineOrder(-940)]
public sealed class KyrolusPropertyAllowListBehavior<TRequest, TResponse>(
    ILogger<KyrolusPropertyAllowListBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger? _logger = logger;

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is IKyrolusPropertyUpdateRequest { AllowedProperties: { } allowed } propertyUpdate)
        {
            foreach (var name in propertyUpdate.UpdatedPropertyNames)
            {
                // Case-insensitive regardless of how the caller built the set: the underlying EF and
                // Marten repositories resolve Updates keys to entity properties with
                // BindingFlags.IgnoreCase, so a case-sensitive allow-list check here could be bypassed
                // by resubmitting a listed name in different casing.
                var isAllowed = false;
                foreach (var candidate in allowed)
                {
                    if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (!isAllowed)
                {
                    _logger?.LogWarning(
                        "[Kyrolus CQRS Security] {RequestType} attempted to update disallowed property '{PropertyName}'",
                        typeof(TRequest).Name,
                        name);

                    throw new KyrolusSecurityException(
                        $"Property '{name}' is not in the allow-list for {typeof(TRequest).Name}.");
                }
            }
        }

        return next(cancellationToken);
    }
}
