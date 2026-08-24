using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions;
using KyrolusSous.Mediator.Abstractions.Attributes;

namespace KyrolusSous.CQRS.Caching;

[PipelineOrder(-300)]
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
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        if (request is not IKyrolusQueryBase)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (request is not ICacheableRequest cacheable || !cacheable.Cacheable)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = cacheKeyProvider.GetCacheKey(request!);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var cached = await cacheProvider.GetAsync<TResponse>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var response = await next(cancellationToken).ConfigureAwait(false);
        if (response is not null)
        {
            await cacheProvider.SetAsync(cacheKey, response, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
