using KyrolusSous.Caching.Abstractions;
using KyrolusSous.RedisCaching.Services;

namespace KyrolusSous.Caching.Redis;

public sealed class RedisCacheProvider(ICacheService cacheService) : ICacheProvider
{
    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default) =>
        cacheService.GetAsync<T>(cacheKey, cancellationToken);

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) =>
        cacheService.SetAsync(cacheKey, value, expirationTime, cancellationToken);

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default) =>
        cacheService.RemoveAsync(cacheKey, cancellationToken);

    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default) =>
        cacheService.ExistsAsync(cacheKey, cancellationToken);

    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default) =>
        cacheService.RemoveKeysByPatternAsync(keyPattern, cancellationToken);
}
