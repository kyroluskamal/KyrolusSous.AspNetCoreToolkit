namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCacheEntryOptionsTests
{
    [Fact(DisplayName = "KyrolusCacheEntryOptions: Default properties should be null")]
    public void Options_Defaults_ShouldBeNull()
    {
        var options = new KyrolusCacheEntryOptions();
        options.AbsoluteExpirationRelativeToNow.ShouldBeNull();
        options.SlidingExpiration.ShouldBeNull();
        options.Jitter.ShouldBeNull();
        options.NegativeExpirationRelativeToNow.ShouldBeNull();
        options.Tags.ShouldBeNull();
        options.Region.ShouldBeNull();
        options.TenantId.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusCacheEntryOptions: Custom properties should be set correctly")]
    public void Options_CustomValues_ShouldBeRetained()
    {
        var tags = new[] { "catalog", "electronics" };
        var options = new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(10),
            Jitter = TimeSpan.FromMinutes(2),
            NegativeExpirationRelativeToNow = TimeSpan.FromSeconds(30),
            Tags = tags,
            Region = "catalog-region",
            TenantId = "tenant-egypt"
        };

        options.AbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromHours(1));
        options.SlidingExpiration.ShouldBe(TimeSpan.FromMinutes(10));
        options.Jitter.ShouldBe(TimeSpan.FromMinutes(2));
        options.NegativeExpirationRelativeToNow.ShouldBe(TimeSpan.FromSeconds(30));
        options.Tags.ShouldBe(tags);
        options.Region.ShouldBe("catalog-region");
        options.TenantId.ShouldBe("tenant-egypt");
    }
}
