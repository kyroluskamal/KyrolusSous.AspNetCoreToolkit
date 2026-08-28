namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A no-op implementation of <see cref="IKyrolusCacheProvider"/> that implements the Null Object Pattern.
/// </summary>
public sealed class KyrolusNullCacheProvider : IKyrolusCacheProvider
{
    /// <summary>
    /// Gets the singleton instance of <see cref="KyrolusNullCacheProvider"/>.
    /// </summary>
    public static KyrolusNullCacheProvider Instance { get; } = new();

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);

    /// <inheritdoc />
    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IDictionary<string, T?>>(new Dictionary<string, T?>());

    /// <inheritdoc />
    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, KyrolusCacheEntryOptions? options = null, CancellationToken cancellationToken = default) =>
        factory(cancellationToken);

    /// <inheritdoc />
    public Task<long> IncrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(value);

    /// <inheritdoc />
    public Task<long> DecrementAsync(string cacheKey, long value = 1, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(-value);

    /// <inheritdoc />
    public Task<bool> HashSetAsync<TField>(string cacheKey, string field, TField value, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<TField?> HashGetAsync<TField>(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult<TField?>(default);

    /// <inheritdoc />
    public Task<IDictionary<string, TField?>> HashGetAllAsync<TField>(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<IDictionary<string, TField?>>(new Dictionary<string, TField?>());

    /// <inheritdoc />
    public Task<bool> HashDeleteAsync(string cacheKey, string field, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
