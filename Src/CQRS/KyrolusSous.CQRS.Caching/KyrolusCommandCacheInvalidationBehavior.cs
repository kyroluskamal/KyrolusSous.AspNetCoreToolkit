using KyrolusSous.CQRS.Abstractions.Security;

namespace KyrolusSous.CQRS.Caching;

/// <summary>
/// Invalidates cached query results after a command that touches the same entity succeeds.
/// </summary>
/// <remarks>
/// <para>
/// Ordered to run outside (before, on the way in; after, on the way out) the EF/Marten transaction
/// behaviors (<c>PipelineOrder(-530)</c>), so invalidation happens only once the write has actually
/// committed. Invalidating first and committing second would let a read that lands in between
/// repopulate the cache from pre-write state, which the later commit can never correct.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> <see cref="KyrolusQueryCachingBehavior{TRequest,TResponse}.ScopeKey"/> stores
/// a non-shared query result under <c>tenant:{T}:user:{U}:{entityKey}</c>. Without knowing the acting
/// tenant, this behavior could only wildcard the bare entity name on both ends
/// (<c>*{pattern}*</c>) - which matches that entity's cached keys for EVERY tenant, not just the one
/// that just wrote. A single tenant's write would silently purge every other tenant's cached reads for
/// the same entity: correct (nothing stale survives) but needlessly wasteful, and a cross-tenant
/// side channel in its own right (tenant A's write activity is observable as tenant B's cache misses).
/// When an <see cref="IKyrolusCurrentUserContext"/> with a resolvable <c>TenantId</c> is available,
/// the pattern is scoped to that tenant's own key prefix (<c>*tenant:{tenant}:*{pattern}*</c>) instead,
/// so a write only ever invalidates its own tenant's cached entries.
/// </para>
/// <para>
/// <b>Documented tradeoff.</b> This does NOT also invalidate the unscoped/shared cache entries a
/// request opted into via <see cref="IKyrolusCacheableRequest.IsSharedAcrossUsers"/> (those are stored
/// under the bare, unprefixed key - see <c>ScopeKey</c> - so a tenant-scoped pattern can never match
/// them). That is accepted deliberately, not an oversight: shared/cross-user caching is an explicit
/// opt-in for data that is intentionally less strictly consistent (a product catalog, a public report),
/// so leaving those entries to expire on their own TTL rather than proactively invalidating them on
/// every write is consistent with what that opt-in already signals. An application that needs a shared
/// entry invalidated immediately on write should not mark the underlying query
/// <c>IsSharedAcrossUsers</c> in the first place.
/// </para>
/// <para>
/// When no <see cref="IKyrolusCurrentUserContext"/> is supplied at all, or it resolves no tenant, this
/// falls back to the original unscoped <c>*{pattern}*</c> pattern - preserving existing behavior for
/// callers who never registered a current-user context (and matching <c>ScopeKey</c>'s own fallback of
/// leaving a request unscoped when no tenant is resolvable).
/// </para>
/// </remarks>
[PipelineOrder(-560)]
public sealed class KyrolusCommandCacheInvalidationBehavior<TRequest, TResponse>(
    IKyrolusCacheKeyProvider cacheKeyProvider,
    IKyrolusCacheProvider? cacheProvider = null,
    IKyrolusCurrentUserContext? userContext = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        // No real provider registered (an app that never called the caching-provider extension) -
        // nothing to invalidate, and this must not throw, since AddKyrolusCqrsCaching registers this
        // behavior for every request type regardless of whether a provider was also registered.
        if (cacheProvider is null || request is not IKyrolusCommandBase)
        {
            return response;
        }

        if (request is not IKyrolusCacheableRequest cacheable || !cacheable.Cacheable)
        {
            return response;
        }

        var pattern = cacheKeyProvider.GetCachePattern(request);
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            // IKyrolusCacheKeyProvider.GetCachePattern returns a bare entity name (e.g. "Order"),
            // not a glob - and KyrolusQueryCachingBehavior stores query results under
            // "tenant:{T}:user:{U}:{entityKey}" (or, for a request opted into
            // IKyrolusCacheableRequest.IsSharedAcrossUsers, the bare key with no such prefix at all).
            // BuildInvalidationPattern picks between a tenant-scoped wildcard and this unscoped
            // fallback - see this type's <remarks> for the full reasoning.
            var wildcardPattern = BuildInvalidationPattern(pattern);
            await cacheProvider.RemoveKeysByPatternAsync(wildcardPattern, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private string BuildInvalidationPattern(string pattern)
    {
        var tenant = userContext?.TenantId;
        return string.IsNullOrWhiteSpace(tenant)
            ? $"*{pattern}*"
            : $"*tenant:{tenant}:*{pattern}*";
    }
}
