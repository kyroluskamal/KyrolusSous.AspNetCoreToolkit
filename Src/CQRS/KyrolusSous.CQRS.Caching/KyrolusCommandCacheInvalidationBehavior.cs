namespace KyrolusSous.CQRS.Caching;

/// <summary>
/// Invalidates cached query results after a command that touches the same entity succeeds.
/// </summary>
/// <remarks>
/// Ordered to run outside (before, on the way in; after, on the way out) the EF/Marten transaction
/// behaviors (<c>PipelineOrder(-530)</c>), so invalidation happens only once the write has actually
/// committed. Invalidating first and committing second would let a read that lands in between
/// repopulate the cache from pre-write state, which the later commit can never correct.
/// </remarks>
[PipelineOrder(-560)]
public sealed class KyrolusCommandCacheInvalidationBehavior<TRequest, TResponse>(
    IKyrolusCacheProvider cacheProvider,
    IKyrolusCacheKeyProvider cacheKeyProvider)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is not IKyrolusCommandBase)
        {
            return response;
        }

        if (request is not ICacheableRequest cacheable || !cacheable.Cacheable)
        {
            return response;
        }

        var pattern = cacheKeyProvider.GetCachePattern(request!);
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            // IKyrolusCacheKeyProvider.GetCachePattern returns a bare entity name (e.g. "Order"),
            // not a glob - and KyrolusQueryCachingBehavior stores query results under
            // "tenant:{T}:user:{U}:{entityKey}" (or, for a request opted into
            // ICacheableRequest.IsSharedAcrossUsers, the bare key with no such prefix at all). A
            // literal, unwrapped "Order" pattern matches neither shape, so RemoveKeysByPatternAsync
            // would silently remove nothing and every write would leave stale cached reads behind
            // regardless of scoping. Wildcarding both ends matches the entity name wherever it sits
            // in the resolved key, scoped or not.
            var wildcardPattern = $"*{pattern}*";
            await cacheProvider.RemoveKeysByPatternAsync(wildcardPattern, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
