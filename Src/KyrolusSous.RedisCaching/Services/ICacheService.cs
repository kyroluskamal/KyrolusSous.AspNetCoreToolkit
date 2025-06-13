namespace KyrolusSous.RedisCaching.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default);
    Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default);
}
