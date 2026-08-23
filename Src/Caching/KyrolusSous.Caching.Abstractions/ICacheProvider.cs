namespace KyrolusSous.Caching.Abstractions;

public interface ICacheProvider
{
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default);
    Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default);
    Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default);
    Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default);
    Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default);
    Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default);
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<long> IncrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(value);
    Task<long> DecrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(-value);
    Task<bool> HashSetAsync<TField>(string cacheKey, string field, TField value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    Task<TField?> HashGetAsync<TField>(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult<TField?>(default);
    Task<IDictionary<string, TField?>> HashGetAllAsync<TField>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<IDictionary<string, TField?>>(new Dictionary<string, TField?>());
    Task<bool> HashDeleteAsync(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
