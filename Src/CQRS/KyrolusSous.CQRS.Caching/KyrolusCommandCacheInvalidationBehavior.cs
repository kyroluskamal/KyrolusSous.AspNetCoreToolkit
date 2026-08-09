namespace KyrolusSous.CQRS.Caching;

public sealed class KyrolusCommandCacheInvalidationBehavior<TRequest, TResponse>(
    ICacheProvider cacheProvider,
    IKyrolusCacheKeyProvider cacheKeyProvider)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

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
            await cacheProvider.RemoveKeysByPatternAsync(pattern, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
