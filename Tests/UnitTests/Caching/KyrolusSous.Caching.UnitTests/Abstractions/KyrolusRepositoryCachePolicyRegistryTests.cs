namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusRepositoryCachePolicyRegistryTests
{
    private sealed class OrderEntity { }

    [Fact(DisplayName = "KyrolusNoopRepositoryCachePolicyProvider: Should return null policy")]
    public async Task NoopProvider_ReturnsNull()
    {
        var provider = KyrolusNoopRepositoryCachePolicyProvider.Instance;
        var context = new KyrolusRepositoryCachePolicyContext(typeof(OrderEntity), "OrderRepo", "GetById");
        var policy = await provider.GetPolicyAsync(context);
        policy.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusRepositoryCachePolicyRegistry: Hierarchical resolution should respect 6-tier precedence")]
    public async Task Registry_HierarchicalResolution_ResolvesCorrectly()
    {
        var defaultPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(5));
        var opPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(10));
        var typeOpPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(15));
        var tenantPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(20));
        var tenantOpPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(25));
        var tenantTypeOpPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(30));

        var registry = new KyrolusRepositoryCachePolicyRegistry()
            .SetDefault(defaultPolicy)
            .SetForOperation("GetById", opPolicy)
            .SetForType<OrderEntity>("GetById", typeOpPolicy)
            .SetForTenant("tenant-a", tenantPolicy)
            .SetForTenantOperation("tenant-a", "GetById", tenantOpPolicy)
            .SetForTenantType<OrderEntity>("tenant-a", "GetById", tenantTypeOpPolicy);

        // 1. Tenant + Type + Op -> tenantTypeOpPolicy (30 min)
        var p1 = await registry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(OrderEntity), TenantId: "tenant-a", Operation: "GetById"));
        p1.ShouldBe(tenantTypeOpPolicy);

        // 2. Tenant + Op (different type) -> tenantOpPolicy (25 min)
        var p2 = await registry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), TenantId: "tenant-a", Operation: "GetById"));
        p2.ShouldBe(tenantOpPolicy);

        // 3. Tenant only (different op) -> tenantPolicy (20 min)
        var p3 = await registry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), TenantId: "tenant-a", Operation: "List"));
        p3.ShouldBe(tenantPolicy);

        // 4. Type + Op (no tenant) -> typeOpPolicy (15 min)
        var p4 = await registry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(OrderEntity), Operation: "GetById"));
        p4.ShouldBe(typeOpPolicy);

        // 5. Op only (no tenant, different type) -> opPolicy (10 min)
        var p5 = await registry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), Operation: "GetById"));
        p5.ShouldBe(opPolicy);

        // 6. Default (different op, different type) -> defaultPolicy (5 min)
        var p6 = await registry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), Operation: "Unknown"));
        p6.ShouldBe(defaultPolicy);
    }

    [Fact(DisplayName = "KyrolusRepositoryCachePolicyRegistry: Setting null or invalid arguments should throw")]
    public void Registry_Validation_Throws()
    {
        var registry = new KyrolusRepositoryCachePolicyRegistry();
        Should.Throw<ArgumentNullException>(() => registry.SetDefault(null!));
        Should.Throw<ArgumentException>(() => registry.SetForOperation("", new KyrolusCachePolicy()));
        Should.Throw<ArgumentException>(() => registry.SetForTenant("", new KyrolusCachePolicy()));
        Should.Throw<ArgumentException>(() => registry.SetForTenantOperation("", "op", new KyrolusCachePolicy()));
        Should.Throw<ArgumentException>(() => registry.SetForTenantOperation("t", "", new KyrolusCachePolicy()));
        Should.Throw<ArgumentException>(() => registry.SetForTenantType<OrderEntity>("", "op", new KyrolusCachePolicy()));
        Should.Throw<ArgumentException>(() => registry.SetForTenantType<OrderEntity>("t", "", new KyrolusCachePolicy()));
        Should.Throw<ArgumentException>(() => registry.SetForType<OrderEntity>("", new KyrolusCachePolicy()));
    }
}
