namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCachePoliciesTests
{
    private sealed record TestProduct(int Id, string Name);

    [Fact(DisplayName = "KyrolusNullCachePolicyProvider: Should always return null policy")]
    public void NullProvider_ReturnsNull()
    {
        var provider = KyrolusNullCachePolicyProvider.Instance;
        provider.GetPolicy(typeof(TestProduct), KyrolusCacheOperation.Get).ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusCachePolicyRegistry: Type-specific policy should take precedence over operation and default policies")]
    public void Registry_PrecedenceOrder_TypeSpecificWins()
    {
        var defaultPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(10));
        var operationPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(20));
        var typePolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(30));

        var registry = new KyrolusCachePolicyRegistry()
            .SetDefault(defaultPolicy)
            .SetForOperation(KyrolusCacheOperation.Get, operationPolicy)
            .SetForType<TestProduct>(KyrolusCacheOperation.Get, typePolicy);

        // 1. For TestProduct + Get -> should return typePolicy (30 min)
        registry.GetPolicy(typeof(TestProduct), KyrolusCacheOperation.Get).ShouldBe(typePolicy);

        // 2. For string + Get -> should return operationPolicy (20 min)
        registry.GetPolicy(typeof(string), KyrolusCacheOperation.Get).ShouldBe(operationPolicy);

        // 3. For string + Set -> should return defaultPolicy (10 min)
        registry.GetPolicy(typeof(string), KyrolusCacheOperation.Set).ShouldBe(defaultPolicy);
    }

    [Fact(DisplayName = "KyrolusCachePolicyRegistry: Setting null policies should throw ArgumentNullException")]
    public void Registry_NullArguments_ThrowException()
    {
        var registry = new KyrolusCachePolicyRegistry();
        Should.Throw<ArgumentNullException>(() => registry.SetDefault(null!));
        Should.Throw<ArgumentNullException>(() => registry.SetForOperation(KyrolusCacheOperation.Get, null!));
        Should.Throw<ArgumentNullException>(() => registry.SetForType<TestProduct>(KyrolusCacheOperation.Get, null!));
    }
}
