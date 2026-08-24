namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class CacheKeyContextTests
{
    private sealed class MinimalKeyContext : ICacheKeyContext
    {
        public string? ScopeKey => "tenant:1";
    }

    private sealed class FullKeyContext : ICacheKeyContext
    {
        public string? ScopeKey => "tenant:2";
        public string? Region => "reg1";
        public string? TenantId => "ten1";
    }

    [Fact(DisplayName = "ICacheKeyContext: Default interface methods should return null for Region and TenantId")]
    public void DefaultInterfaceMethods_ReturnNull()
    {
        ICacheKeyContext context = new MinimalKeyContext();
        context.ScopeKey.ShouldBe("tenant:1");
        context.Region.ShouldBeNull();
        context.TenantId.ShouldBeNull();
    }

    [Fact(DisplayName = "ICacheKeyContext: Overridden properties should return specified Region and TenantId")]
    public void OverriddenProperties_ReturnValues()
    {
        ICacheKeyContext context = new FullKeyContext();
        context.ScopeKey.ShouldBe("tenant:2");
        context.Region.ShouldBe("reg1");
        context.TenantId.ShouldBe("ten1");
    }
}
