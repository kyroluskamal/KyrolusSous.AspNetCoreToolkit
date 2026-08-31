namespace KyrolusSous.Validation.Caching.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddKyrolusValidationDistributedCache replaces an already-registered IKyrolusValidationCacheStore")]
    public void AddKyrolusValidationDistributedCache_ReplacesExistingRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IKyrolusCacheProvider>());
        services.AddSingleton<IKyrolusValidationCacheStore, PreExistingCacheStore>();

        services.AddKyrolusValidationDistributedCache();

        var provider = services.BuildServiceProvider();
        var cacheStore = provider.GetRequiredService<IKyrolusValidationCacheStore>();

        cacheStore.ShouldBeOfType<KyrolusValidationDistributedCacheStore>();
    }

    [Fact(DisplayName = "AddKyrolusValidationDistributedCache registers IKyrolusValidationCacheStore even when nothing was registered before")]
    public void AddKyrolusValidationDistributedCache_RegistersStore_WhenNoneExisted()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IKyrolusCacheProvider>());

        services.AddKyrolusValidationDistributedCache();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IKyrolusValidationCacheStore>()
            .ShouldBeOfType<KyrolusValidationDistributedCacheStore>();
    }

    private sealed class PreExistingCacheStore : IKyrolusValidationCacheStore
    {
        public ValueTask<IReadOnlyList<KyrolusValidationFailure>?> TryGetAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>?>(null);

        public ValueTask SetAsync(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
