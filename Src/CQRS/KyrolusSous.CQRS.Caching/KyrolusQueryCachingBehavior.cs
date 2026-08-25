using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions;
using KyrolusSous.Mediator.Abstractions.Attributes;

namespace KyrolusSous.CQRS.Caching;

[PipelineOrder(-300)]
public sealed class KyrolusQueryCachingBehavior<TRequest, TResponse>(
    ICacheProvider? cacheProvider = null,
    IKyrolusCacheKeyProvider? cacheKeyProvider = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly ICacheProvider? _cacheProvider = cacheProvider;
    private readonly IKyrolusCacheKeyProvider _cacheKeyProvider = cacheKeyProvider ?? new KyrolusDefaultCacheKeyProvider();

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

        if (request is not ICacheableRequest cacheable || !cacheable.Cacheable)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = _cacheKeyProvider.GetCacheKey(request!);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

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
}
