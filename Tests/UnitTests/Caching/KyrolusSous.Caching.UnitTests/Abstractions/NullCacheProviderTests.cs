namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class NullCacheProviderTests
{
    [Fact(DisplayName = "NullCacheProvider: All operations should execute as safe no-ops")]
    public async Task NullCacheProvider_AllMethods_ExecuteSafely()
    {
        var provider = NullCacheProvider.Instance;

        // Get
        var getResult = await provider.GetAsync<string>("key1");
        getResult.ShouldBeNull();

        // Set
        await provider.SetAsync("key1", "val1", TimeSpan.FromMinutes(1));
        await provider.SetAsync("key1", "val1", new KyrolusCacheEntryOptions());

        // Remove
        await provider.RemoveAsync("key1");

        // Exists
        var exists = await provider.ExistsAsync("key1");
        exists.ShouldBeFalse();

        // Pattern & Tags
        await provider.RemoveKeysByPatternAsync("key:*");
        await provider.RemoveByTagAsync("tag1");

        // Batch operations
        var getMany = await provider.GetManyAsync<string>(["k1", "k2"]);
        getMany.ShouldBeEmpty();

        await provider.SetManyAsync([new KeyValuePair<string, string>("k1", "v1")]);
        await provider.SetManyAsync([new KeyValuePair<string, string>("k1", "v1")], new KyrolusCacheEntryOptions());
        await provider.RemoveManyAsync(["k1", "k2"]);

        // GetOrCreate executes factory directly
        var computed = await provider.GetOrCreateAsync("calc:key", _ => Task.FromResult(42));
        computed.ShouldBe(42);

        // Atomic counters
        var inc = await provider.IncrementAsync("counter", 5);
        inc.ShouldBe(5);

        var dec = await provider.DecrementAsync("counter", 3);
        dec.ShouldBe(-3);

        // Hashes
        var hashSet = await provider.HashSetAsync("hkey", "field1", "val1");
        hashSet.ShouldBeTrue();

        var hashGet = await provider.HashGetAsync<string>("hkey", "field1");
        hashGet.ShouldBeNull();

        var hashGetAll = await provider.HashGetAllAsync<string>("hkey");
        hashGetAll.ShouldBeEmpty();

        var hashDel = await provider.HashDeleteAsync("hkey", "field1");
        hashDel.ShouldBeTrue();
    }
}
