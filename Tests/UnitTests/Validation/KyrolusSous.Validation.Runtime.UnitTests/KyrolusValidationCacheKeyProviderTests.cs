namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationCacheKeyProviderTests
{
    [Theory(DisplayName = "GetCacheEntry returns null for invalid requests")]
    [InlineData(InvalidRequestKind.NullRequest)]
    [InlineData(InvalidRequestKind.NotCacheableObject)]
    [InlineData(InvalidRequestKind.NullCacheKey)]
    [InlineData(InvalidRequestKind.EmptyCacheKey)]
    [InlineData(InvalidRequestKind.CacheModeIsNone)]
    [InlineData(InvalidRequestKind.ZeroTtl)]
    public void GetCacheEntry_ReturnsNull_WhenRequestIsInvalid(InvalidRequestKind kind)
    {
        object? request = kind switch
        {
            InvalidRequestKind.NullRequest => null,
            InvalidRequestKind.NotCacheableObject => new object(),
            InvalidRequestKind.NullCacheKey => new CacheableNullKeyRequest(),
            InvalidRequestKind.EmptyCacheKey => new CacheableEmptyStringKeyRequest(),
            InvalidRequestKind.CacheModeIsNone => new CacheableCacheModeIsNoneRequest(),
            InvalidRequestKind.ZeroTtl => new ZeroTtlCacheableTestRequest(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var provider = new KyrolusValidationCacheKeyProvider();
        var result = provider.GetCacheEntry(request!, KyrolusValidationContext.Default);
        result.ShouldBeNull();
    }

    [Fact(DisplayName = "GetCacheEntry should return KyrolusValidationCacheEntry object for valid request")]
    public void GetCacheEntry_ShouldReturn_KyrolusValidationCacheEntry_forValidRequest()
    {
        var provider = new KyrolusValidationCacheKeyProvider();
        var result = provider.GetCacheEntry(new ValidCacheableTestRequest(), KyrolusValidationContext.Default);
        result.ShouldNotBeNull();
        result.Key.ShouldBe("ValidCache");
        result.Mode.ShouldBe(KyrolusValidationCacheMode.FailuresOnly);
        result.Ttl.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact(DisplayName = "GetCacheEntry uses DefaultTtl when CacheTtl is null")]
    public void GetCacheEntry_UsesDefaultTtl_WhenCacheTtlIsNull()
    {
        var provider = new KyrolusValidationCacheKeyProvider();
        var result = provider.GetCacheEntry(new ValidCacheableWithNullTtlRequest(), KyrolusValidationContext.Default);
        result.ShouldNotBeNull();
        result.Key.ShouldBe("ValidCacheNullTtl");
        result.Mode.ShouldBe(KyrolusValidationCacheMode.All);
        result.Ttl.ShouldBe(KyrolusValidationCacheDefaults.DefaultTtl);
    }
}

