using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.Mediator.Abstractions;
using KyrolusSous.Mediator.Abstractions.Attributes;

namespace KyrolusSous.CQRS.Caching;

[PipelineOrder(-300)]
public sealed class KyrolusQueryCachingBehavior<TRequest, TResponse>(
    IKyrolusCacheProvider? cacheProvider = null,
    IKyrolusCacheKeyProvider? cacheKeyProvider = null,
    IKyrolusCurrentUserContext? userContext = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusCacheProvider? _cacheProvider = cacheProvider;
    private readonly IKyrolusCacheKeyProvider _cacheKeyProvider = cacheKeyProvider ?? new KyrolusDefaultCacheKeyProvider();
    private readonly IKyrolusCurrentUserContext? _userContext = userContext;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        if (request is not IKyrolusQueryBase || _cacheProvider is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (request is not IKyrolusCacheableRequest cacheable || !cacheable.Cacheable)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = _cacheKeyProvider.GetCacheKey(request);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        cacheKey = ScopeKey(cacheKey, cacheable);

        var cached = await _cacheProvider.GetAsync<TResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var response = await next(cancellationToken).ConfigureAwait(false);
        if (response is not null)
        {
            await _cacheProvider.SetAsync(cacheKey, response, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Prefixes the entity-derived cache key with the current tenant/user unless the request opts
    /// into a shared cache via <see cref="IKyrolusCacheableRequest.IsSharedAcrossUsers"/>.
    /// </summary>
    /// <remarks>
    /// Without this, the default cache key is built purely from the request's own shape (entity name,
    /// id, page) with no notion of who is asking - so a per-user query (a profile, "my orders") cached
    /// by one caller would be served straight back to the next caller who happens to send the same
    /// request shape, regardless of tenant or identity. Scoping by tenant+user is the safe default;
    /// <see cref="IKyrolusCacheableRequest.IsSharedAcrossUsers"/> is the explicit opt-out for data that really
    /// is the same for everyone (a product catalog, a public report).
    /// </remarks>
    private string ScopeKey(string cacheKey, IKyrolusCacheableRequest cacheable)
    {
        if (cacheable.IsSharedAcrossUsers || _userContext is null)
        {
            return cacheKey;
        }

        var tenant = string.IsNullOrWhiteSpace(_userContext.TenantId) ? "-" : _userContext.TenantId;
        var user = string.IsNullOrWhiteSpace(_userContext.UserId) ? "-" : _userContext.UserId;
        return $"tenant:{tenant}:user:{user}:{cacheKey}";
    }
}
