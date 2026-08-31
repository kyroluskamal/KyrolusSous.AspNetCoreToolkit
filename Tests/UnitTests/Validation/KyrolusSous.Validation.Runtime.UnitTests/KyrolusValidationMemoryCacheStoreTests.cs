namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationMemoryCacheStoreTests
{
    [Theory(DisplayName = "KyrolusValidationMemoryCacheStore should return null if the key is null or whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task KyrolusValidationMemoryCacheStore_ShouldReturn_null_whenKeyIsNullOrEmptyString(string? value)
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        var result = await cacheStore.TryGetAsync(value!);
        result.ShouldBeNull();
    }

    [Fact(DisplayName = "TryGetAsync returns null when key is not found in cache")]
    public async Task TryGetAsync_ReturnsNull_WhenKeyNotFound()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        var result = await cacheStore.TryGetAsync("non-existing-key");

        result.ShouldBeNull();
    }

    [Fact(DisplayName = "TryGetAsync returns null and removes entry when entry is expired")]
    public async Task TryGetAsync_ReturnsNullAndRemovesEntry_WhenExpired()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        await cacheStore.SetAsync("expired-key", [new KyrolusValidationFailure("Prop", "Error")], TimeSpan.FromMilliseconds(10));

        await Task.Delay(30);

        var result = await cacheStore.TryGetAsync("expired-key");
        result.ShouldBeNull();
    }

    [Fact(DisplayName = "TryGetAsync returns cached failures when valid entry exists")]
    public async Task TryGetAsync_ReturnsFailures_WhenValidEntryExists()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        IReadOnlyList<KyrolusValidationFailure> expectedFailures = [new KyrolusValidationFailure("Age", "Invalid age")];

        await cacheStore.SetAsync("valid-key", expectedFailures, TimeSpan.FromMinutes(5));

        var result = await cacheStore.TryGetAsync("valid-key");

        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].PropertyName.ShouldBe("Age");
    }

    [Theory(DisplayName = "SetAsync does not store entry when key is null or whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAsync_DoesNotStore_WhenKeyIsNullOrEmptyString(string? key)
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        await cacheStore.SetAsync(key!, [new KyrolusValidationFailure("Prop", "Error")], TimeSpan.FromMinutes(5));

        var result = await cacheStore.TryGetAsync("   ");
        result.ShouldBeNull();
    }

    [Fact(DisplayName = "SetAsync does not store entry when TTL is zero or negative")]
    public async Task SetAsync_DoesNotStore_WhenTtlIsZeroOrNegative()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        await cacheStore.SetAsync("zero-ttl-key", [new KyrolusValidationFailure("Prop", "Error")], TimeSpan.Zero);

        var result = await cacheStore.TryGetAsync("zero-ttl-key");
        result.ShouldBeNull();
    }
}
