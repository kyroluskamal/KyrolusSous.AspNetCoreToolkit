namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisCacheDependenciesTests
{
    [Fact(DisplayName = "KyrolusRedisCacheDependencies: Default instance should have non-null components")]
    public void DefaultDependencies_ShouldHaveNonNullComponents()
    {
        var deps = KyrolusRedisCacheDependencies.Default;

        deps.Serializer.ShouldNotBeNull();
        deps.KeyFactory.ShouldNotBeNull();
        deps.Options.ShouldNotBeNull();
        deps.Observer.ShouldNotBeNull();
        deps.PolicyProvider.ShouldNotBeNull();
    }
}
