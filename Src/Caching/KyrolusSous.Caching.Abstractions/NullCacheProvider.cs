namespace KyrolusSous.Caching.Abstractions;

public sealed class NullCacheProvider : ICacheProvider
{
    public static NullCacheProvider Instance { get; } = new();

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IDictionary<string, T?>>(new Dictionary<string, T?>());
    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, KyrolusCacheEntryOptions? options = null, CancellationToken cancellationToken = default) =>
        factory(cancellationToken);

    public Task<long> IncrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(value);
    public Task<long> DecrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(-value);
    public Task<bool> HashSetAsync<TField>(string cacheKey, string field, TField value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<TField?> HashGetAsync<TField>(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult<TField?>(default);
    public Task<IDictionary<string, TField?>> HashGetAllAsync<TField>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<IDictionary<string, TField?>>(new Dictionary<string, TField?>());
    public Task<bool> HashDeleteAsync(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
