namespace KyrolusSous.CQRS.Caching;

public sealed class KyrolusQueryCachingBehavior<TRequest, TResponse>(
    ICacheProvider cacheProvider,
    IKyrolusCacheKeyProvider cacheKeyProvider)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IKyrolusQueryBase)
        {
            return await next();
        }

        if (request is not ICacheableRequest cacheable || !cacheable.Cacheable)
        {
            return await next();
        }

        var cacheKey = cacheKeyProvider.GetCacheKey(request!);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return await next();
        }

        if (await cacheProvider.ExistsAsync(cacheKey, cancellationToken).ConfigureAwait(false))
        {
            var cached = await cacheProvider.GetAsync<TResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }
        }

        var response = await next();
        await cacheProvider.SetAsync(cacheKey, response, cancellationToken: cancellationToken).ConfigureAwait(false);
        return response;
    }
}
