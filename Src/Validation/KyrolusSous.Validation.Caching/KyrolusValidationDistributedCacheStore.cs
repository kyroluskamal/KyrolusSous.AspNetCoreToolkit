namespace KyrolusSous.Validation.Caching;

/// <summary>
/// Backs <see cref="IKyrolusValidationCacheStore"/> with an <see cref="IKyrolusCacheProvider"/>, so validation
/// result caching is shared across every instance of the app instead of living in one process's memory.
/// </summary>
public sealed class KyrolusValidationDistributedCacheStore(IKyrolusCacheProvider cacheProvider) : IKyrolusValidationCacheStore
{
    private readonly IKyrolusCacheProvider cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));

    public async ValueTask<IReadOnlyList<KyrolusValidationFailure>?> TryGetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        return await cacheProvider.GetAsync<IReadOnlyList<KyrolusValidationFailure>>(key, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetAsync(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || ttl <= TimeSpan.Zero) return;

        await cacheProvider.SetAsync(key, failures, ttl, cancellationToken).ConfigureAwait(false);
    }
}
