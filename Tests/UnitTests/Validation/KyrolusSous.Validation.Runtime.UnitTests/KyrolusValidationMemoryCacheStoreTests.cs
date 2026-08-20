namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationMemoryCacheStoreTests
{
    [Theory(DisplayName = "KyrolusValidationMemoryCacheStore should return false and empty failures array if the key is null or whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void KyrolusValidationMemoryCacheStore_ShouldReturn_false_EmptyFailures_whenKeyIsNullOrEmptyString(string? value)
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        var result = cacheStore.TryGet(value!, out var failures);
        result.ShouldBeFalse();
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "TryGet returns false and empty failures when key is not found in cache")]
    public void TryGet_ReturnsFalse_WhenKeyNotFound()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        var result = cacheStore.TryGet("non-existing-key", out var failures);

        result.ShouldBeFalse();
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "TryGet returns false and removes entry when entry is expired")]
    public async Task TryGet_ReturnsFalseAndRemovesEntry_WhenExpired()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        cacheStore.Set("expired-key", [new KyrolusValidationFailure("Prop", "Error")], TimeSpan.FromMilliseconds(10));

        await Task.Delay(30);

        var result = cacheStore.TryGet("expired-key", out var failures);
        result.ShouldBeFalse();
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "TryGet returns true and cached failures when valid entry exists")]
    public void TryGet_ReturnsTrue_WhenValidEntryExists()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        IReadOnlyList<KyrolusValidationFailure> expectedFailures = [new KyrolusValidationFailure("Age", "Invalid age")];

        cacheStore.Set("valid-key", expectedFailures, TimeSpan.FromMinutes(5));

        var result = cacheStore.TryGet("valid-key", out var actualFailures);

        result.ShouldBeTrue();
        actualFailures.ShouldNotBeNull();
        actualFailures.Count.ShouldBe(1);
        actualFailures[0].PropertyName.ShouldBe("Age");
    }

    [Theory(DisplayName = "Set does not store entry when key is null or whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Set_DoesNotStore_WhenKeyIsNullOrEmptyString(string? key)
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        cacheStore.Set(key!, [new KyrolusValidationFailure("Prop", "Error")], TimeSpan.FromMinutes(5));

        var result = cacheStore.TryGet("   ", out _);
        result.ShouldBeFalse();
    }

    [Fact(DisplayName = "Set does not store entry when TTL is zero or negative")]
    public void Set_DoesNotStore_WhenTtlIsZeroOrNegative()
    {
        var cacheStore = new KyrolusValidationMemoryCacheStore();
        cacheStore.Set("zero-ttl-key", [new KyrolusValidationFailure("Prop", "Error")], TimeSpan.Zero);

        var result = cacheStore.TryGet("zero-ttl-key", out var failures);
        result.ShouldBeFalse();
        failures.ShouldBeEmpty();
    }
}

