using KyrolusSous.RedisCaching.Services;

namespace KyrolusSous.CQRS.Base.Query;

public class GetFromCacheCommon<TResponse>(ICacheService cacheService)
where TResponse : class
{
    public async Task<TResponse> GetFromCache(string cacheKey, bool cacheable, Func<Task<TResponse>> getData, CancellationToken cancellationToken)
    {
        if (cacheable)
        {
            var cachedData = await cacheService.GetAsync<TResponse>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                return cachedData;
            }
        }
        var data = await getData();
        if (cacheable)
        {
            await cacheService.SetAsync(cacheKey, data, cancellationToken: cancellationToken);
        }
        return data;
    }
}
