namespace KyrolusSous.Repositories.EF.Generator.TestApp;

public sealed class InMemoryCacheProvider : ICacheProvider
{
    private sealed record Entry(object? Value, DateTimeOffset? ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var existing))
        {
            if (existing.ExpiresAt is null || existing.ExpiresAt > DateTimeOffset.UtcNow)
                return Task.FromResult((T?)existing.Value);

            _store.TryRemove(key, out _);
        }

        return CreateAndStoreAsync();

        async Task<T?> CreateAndStoreAsync()
        {
            var value = await factory(cancellationToken).ConfigureAwait(false);

            DateTimeOffset? expires = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : null;
            _store[key] = new Entry(value, expires);

            return value;
        }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public void Clear() => _store.Clear();
    public int Count => _store.Count;
}
