namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCacheKeyFactoryTests
{
    [Fact(DisplayName = "KyrolusCacheKeyFactory: BuildKey without prefix should format key with region and tenant")]
    public void BuildKey_NoPrefix_FormatsCorrectly()
    {
        var factory = new KyrolusCacheKeyFactory();

        factory.BuildKey("user:101").ShouldBe("user:101");
        factory.BuildKey("user:101", region: "identity").ShouldBe("identity:user:101");
        factory.BuildKey("user:101", region: "identity", tenantId: "tenant1").ShouldBe("identity:tenant1:user:101");
        factory.BuildKey("user:101", region: null, tenantId: "tenant1").ShouldBe("tenant1:user:101");
    }

    [Fact(DisplayName = "KyrolusCacheKeyFactory: BuildKey with prefix should include prefix segment")]
    public void BuildKey_WithPrefix_IncludesPrefix()
    {
        var factory = new KyrolusCacheKeyFactory("myapp");

        factory.BuildKey("user:101").ShouldBe("myapp:user:101");
        factory.BuildKey("user:101", region: "identity").ShouldBe("myapp:identity:user:101");
        factory.BuildKey("user:101", region: "identity", tenantId: "tenant1").ShouldBe("myapp:identity:tenant1:user:101");
    }

    [Fact(DisplayName = "KyrolusCacheKeyFactory: BuildTagKey should format tag correctly")]
    public void BuildTagKey_FormatsCorrectly()
    {
        var factory = new KyrolusCacheKeyFactory("myapp");

        factory.BuildTagKey("products").ShouldBe("myapp:tag:products");
        factory.BuildTagKey("products", region: "catalog", tenantId: "t1").ShouldBe("myapp:catalog:t1:tag:products");
    }

    [Fact(DisplayName = "KyrolusCacheKeyFactory: BuildEntryTagsKey should format reverse lookup key correctly")]
    public void BuildEntryTagsKey_FormatsCorrectly()
    {
        var factory = new KyrolusCacheKeyFactory("myapp");

        factory.BuildEntryTagsKey("user:101").ShouldBe("myapp:tags:user:101");
    }

    [Theory(DisplayName = "KyrolusCacheKeyFactory: Null or whitespace keys should throw ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildKey_InvalidKey_ThrowsArgumentException(string? invalidKey)
    {
        var factory = new KyrolusCacheKeyFactory();
        Should.Throw<ArgumentException>(() => factory.BuildKey(invalidKey!));
        Should.Throw<ArgumentException>(() => factory.BuildTagKey(invalidKey!));
        Should.Throw<ArgumentException>(() => factory.BuildEntryTagsKey(invalidKey!));
    }
}
