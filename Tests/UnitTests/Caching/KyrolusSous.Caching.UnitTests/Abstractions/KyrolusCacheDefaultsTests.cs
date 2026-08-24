namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCacheDefaultsTests
{
    [Fact(DisplayName = "KyrolusCacheDefaults: Should have expected production default values")]
    public void Defaults_ShouldHaveExpectedValues()
    {
        KyrolusCacheDefaults.DefaultTtl.ShouldBe(TimeSpan.FromMinutes(30));
        KyrolusCacheDefaults.DefaultSlidingTtl.ShouldBe(TimeSpan.FromMinutes(5));
        KyrolusCacheDefaults.DefaultLockTtl.ShouldBe(TimeSpan.FromSeconds(10));
        KyrolusCacheDefaults.DefaultLockWait.ShouldBe(TimeSpan.FromSeconds(2));
        KyrolusCacheDefaults.DefaultLockRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
        KyrolusCacheDefaults.DefaultCompressionThresholdBytes.ShouldBe(1024);
        KyrolusCacheDefaults.DefaultCompressionLevel.ShouldBe(CompressionLevel.Fastest);
    }
}
