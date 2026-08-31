namespace KyrolusSous.Validation.Caching.UnitTests;

public class KyrolusValidationDistributedCacheStoreTests
{
    [Fact(DisplayName = "Constructor throws when cacheProvider is null")]
    public void Constructor_Throws_WhenCacheProviderIsNull()
    {
        Should.Throw<ArgumentNullException>(() => new KyrolusValidationDistributedCacheStore(null!));
    }

    [Theory(DisplayName = "TryGetAsync returns null without calling the provider when key is null or whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryGetAsync_ReturnsNull_WhenKeyIsNullOrEmptyString(string? key)
    {
        var provider = Substitute.For<IKyrolusCacheProvider>();
        var store = new KyrolusValidationDistributedCacheStore(provider);

        var result = await store.TryGetAsync(key!);

        result.ShouldBeNull();
        await provider.DidNotReceive().GetAsync<IReadOnlyList<KyrolusValidationFailure>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "TryGetAsync returns whatever the cache provider returns for the key")]
    public async Task TryGetAsync_ReturnsProviderResult()
    {
        IReadOnlyList<KyrolusValidationFailure> expected = [new KyrolusValidationFailure("Age", "Invalid age")];
        var provider = Substitute.For<IKyrolusCacheProvider>();
        provider.GetAsync<IReadOnlyList<KyrolusValidationFailure>>("key", Arg.Any<CancellationToken>())
            .Returns(expected);

        var store = new KyrolusValidationDistributedCacheStore(provider);
        var result = await store.TryGetAsync("key");

        result.ShouldBe(expected);
    }

    [Theory(DisplayName = "SetAsync does not call the provider when key is null/whitespace or TTL is zero or negative")]
    [InlineData(null, 5)]
    [InlineData("", 5)]
    [InlineData("   ", 5)]
    [InlineData("valid-key", 0)]
    [InlineData("valid-key", -1)]
    public async Task SetAsync_DoesNotStore_WhenKeyOrTtlInvalid(string? key, int ttlSeconds)
    {
        var provider = Substitute.For<IKyrolusCacheProvider>();
        var store = new KyrolusValidationDistributedCacheStore(provider);

        await store.SetAsync(key!, [new KyrolusValidationFailure("Prop", "Error")], TimeSpan.FromSeconds(ttlSeconds));

        await provider.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<KyrolusValidationFailure>>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "SetAsync forwards the key, failures and TTL to the cache provider")]
    public async Task SetAsync_ForwardsToProvider()
    {
        var provider = Substitute.For<IKyrolusCacheProvider>();
        var store = new KyrolusValidationDistributedCacheStore(provider);
        IReadOnlyList<KyrolusValidationFailure> failures = [new KyrolusValidationFailure("Prop", "Error")];
        var ttl = TimeSpan.FromMinutes(5);

        await store.SetAsync("key", failures, ttl);

        await provider.Received(1).SetAsync("key", failures, ttl, Arg.Any<CancellationToken>());
    }
}
