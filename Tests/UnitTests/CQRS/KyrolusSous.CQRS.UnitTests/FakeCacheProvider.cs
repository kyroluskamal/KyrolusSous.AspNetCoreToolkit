using System.Collections.Concurrent;
using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// A real (non-mocked) in-memory <see cref="IKyrolusCacheProvider"/> used by tests that need the
/// default-interface members (like <c>SetIfNotExistsAsync</c>) to behave for real rather than being
/// intercepted by a mocking framework, which would otherwise require stubbing every call site
/// individually and risks testing the stub instead of the actual state machine under test.
/// </summary>
internal sealed class FakeCacheProvider : IKyrolusCacheProvider
{
    private readonly ConcurrentDictionary<string, object?> _store = new(StringComparer.Ordinal);

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(cacheKey, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        _store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        _store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(cacheKey));

    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
        => Task.FromResult<IDictionary<string, T?>>(new Dictionary<string, T?>());

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, KyrolusCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
        => factory(cancellationToken);

    /// <summary>Directly seeds a raw value, bypassing the normal Set path - used to simulate another caller's in-flight claim.</summary>
    public void Seed(string cacheKey, object? value) => _store[cacheKey] = value;

    public bool ContainsKey(string cacheKey) => _store.ContainsKey(cacheKey);
}
