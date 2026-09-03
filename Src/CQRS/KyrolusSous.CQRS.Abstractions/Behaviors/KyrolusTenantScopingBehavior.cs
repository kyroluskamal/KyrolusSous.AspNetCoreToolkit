using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior rejecting a request that names a tenant other than the current user's.
/// </summary>
/// <remarks>
/// Opt-in via <see cref="ITenantScopedRequest"/>: a request that does not implement it is untouched by
/// this behavior. This is deliberately a guard, not a filter - it does not scope queries or stamp a
/// tenant onto anything by itself, because doing that generically at the pipeline level would mean
/// reaching into EF/Marten query construction from a package that knows nothing about either. What it
/// does is close the specific, common hole where a tenant id travels as plain request data: without
/// this check, nothing stops an authenticated user of tenant A from sending a request whose
/// <see cref="ITenantScopedRequest.TenantId"/> names tenant B and having the handler act on it,
/// because most handlers trust the request's own field rather than re-deriving the tenant from the
/// caller's identity on every call.
/// </remarks>
[PipelineOrder(-1040)]
public sealed class KyrolusTenantScopingBehavior<TRequest, TResponse>(
    IKyrolusCurrentUserContext? userContext = null,
    ILogger<KyrolusTenantScopingBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusCurrentUserContext? _userContext = userContext;
    private readonly ILogger? _logger = logger;

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is ITenantScopedRequest { TenantId: { Length: > 0 } requestedTenant })
        {
            var currentTenant = _userContext?.TenantId;

            // Fails closed: a request naming a tenant with no usable current tenant to compare
            // against (context missing, or the authenticated caller has no tenant claim at all - a
            // misconfigured identity provider, or simply an unauthenticated call slipping past
            // whatever ran before this behavior) is rejected rather than let through unchecked. The
            // previous version only compared when BOTH sides were non-empty, so a caller with no
            // tenant claim bypassed the guard entirely instead of being the case it exists for.
            if (string.IsNullOrEmpty(currentTenant) || !string.Equals(requestedTenant, currentTenant, StringComparison.Ordinal))
            {
                _logger?.LogWarning(
                    "[Kyrolus CQRS Security] User '{UserId}' of tenant '{CurrentTenant}' attempted to access tenant '{RequestedTenant}' via {RequestType}",
                    _userContext?.UserId,
                    currentTenant ?? "(none)",
                    requestedTenant,
                    typeof(TRequest).Name);

                throw new KyrolusSecurityException(
                    $"Request for tenant '{requestedTenant}' is not permitted for the current tenant '{currentTenant ?? "(none)"}'.");
            }
        }

        return next(cancellationToken);
    }
}
