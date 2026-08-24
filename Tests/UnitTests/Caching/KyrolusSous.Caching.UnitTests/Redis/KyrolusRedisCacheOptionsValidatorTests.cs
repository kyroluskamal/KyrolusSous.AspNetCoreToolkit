namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisCacheOptionsValidatorTests
{
    [Fact(DisplayName = "KyrolusRedisCacheOptionsValidator: Default options should validate successfully")]
    public void DefaultOptions_Validate_Succeeds()
    {
        var options = new KyrolusRedisCacheOptions();
        Should.NotThrow(() => KyrolusRedisCacheOptionsValidator.Validate(options));
    }

    [Theory(DisplayName = "KyrolusRedisCacheOptionsValidator: Non-positive BatchSize or TTLs should throw ArgumentOutOfRangeException")]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidNumbers_Throws(int invalidValue)
    {
        var opt1 = new KyrolusRedisCacheOptions { BatchSize = invalidValue };
        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRedisCacheOptionsValidator.Validate(opt1));

        var opt2 = new KyrolusRedisCacheOptions { DefaultTtl = TimeSpan.FromSeconds(invalidValue) };
        Should.Throw<ArgumentOutOfRangeException>(() => KyrolusRedisCacheOptionsValidator.Validate(opt2));
    }

    [Fact(DisplayName = "KyrolusRedisCacheOptionsValidator: Invalid encryption configurations should throw ArgumentException")]
    public void InvalidEncryption_Throws()
    {
        // 1. Encryption enabled without key
        var opt1 = new KyrolusRedisCacheOptions { EnableEncryption = true };
        Should.Throw<ArgumentException>(() => KyrolusRedisCacheOptionsValidator.Validate(opt1));

        // 2. Key with invalid length (e.g. 10 bytes)
        var opt2 = new KyrolusRedisCacheOptions
        {
            EnableEncryption = true,
            EncryptionKey = new byte[10]
        };
        Should.Throw<ArgumentException>(() => KyrolusRedisCacheOptionsValidator.Validate(opt2));

        // 3. Invalid Base64 string
        var opt3 = new KyrolusRedisCacheOptions
        {
            EnableEncryption = true,
            EncryptionKeyBase64 = "not-valid-base64!!"
        };
        Should.Throw<ArgumentException>(() => KyrolusRedisCacheOptionsValidator.Validate(opt3));
    }

    [Fact(DisplayName = "KyrolusRedisCacheOptionsValidator: RequireRegion without DefaultRegion should throw InvalidOperationException")]
    public void RequireRegion_WithoutDefault_Throws()
    {
        var opt = new KyrolusRedisCacheOptions
        {
            RequireRegion = true,
            DefaultRegion = null
        };
        Should.Throw<InvalidOperationException>(() => KyrolusRedisCacheOptionsValidator.Validate(opt));
    }

    [Fact(DisplayName = "KyrolusRedisCacheOptionsValidator: NearCache options validation")]
    public void NearCacheOptions_Validate()
    {
        var valid = new KyrolusRedisNearCacheOptions();
        Should.NotThrow(() => KyrolusRedisCacheOptionsValidator.Validate(valid));

        var invalid = new KyrolusRedisNearCacheOptions { InvalidationChannel = "" };
        Should.Throw<ArgumentException>(() => KyrolusRedisCacheOptionsValidator.Validate(invalid));
    }
}
