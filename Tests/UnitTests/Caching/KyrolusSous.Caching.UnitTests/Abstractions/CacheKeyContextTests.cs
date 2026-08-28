namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class CacheKeyContextTests
{
    private sealed class MinimalKeyContext : IKyrolusCacheKeyContext
    {
        public string? ScopeKey => "tenant:1";
    }

    private sealed class FullKeyContext : IKyrolusCacheKeyContext
    {
        public string? ScopeKey => "tenant:2";
        public string? Region => "reg1";
        public string? TenantId => "ten1";
    }

    [Fact(DisplayName = "IKyrolusCacheKeyContext: Default interface methods should return null for Region and TenantId")]
    public void DefaultInterfaceMethods_ReturnNull()
    {
        IKyrolusCacheKeyContext context = new MinimalKeyContext();
        context.ScopeKey.ShouldBe("tenant:1");
        context.Region.ShouldBeNull();
        context.TenantId.ShouldBeNull();
    }

    [Fact(DisplayName = "IKyrolusCacheKeyContext: Overridden properties should return specified Region and TenantId")]
    public void OverriddenProperties_ReturnValues()
    {
        IKyrolusCacheKeyContext context = new FullKeyContext();
        context.ScopeKey.ShouldBe("tenant:2");
        context.Region.ShouldBe("reg1");
        context.TenantId.ShouldBe("ten1");
    }
}
