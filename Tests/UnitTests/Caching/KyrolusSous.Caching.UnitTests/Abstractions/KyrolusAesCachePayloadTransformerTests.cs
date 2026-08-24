using System.Security.Cryptography;

namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusAesCachePayloadTransformerTests
{
    [Fact(DisplayName = "KyrolusAesCachePayloadTransformer: Valid AES-256 key should encrypt and decrypt payload successfully")]
    public void Aes256_Roundtrip_Success()
    {
        var key = RandomNumberGenerator.GetBytes(32); // 256-bit key
        var transformer = new KyrolusAesCachePayloadTransformer(key);
        var original = Encoding.UTF8.GetBytes("Super secret payload for user credit card credentials 1234-5678-9012-3456");

        var encrypted = transformer.Transform(original);
        encrypted.ShouldNotBeNull();
        encrypted.Length.ShouldBeGreaterThan(original.Length); // IV + PKCS7 block padding
        encrypted.ShouldNotBe(original); // Verify actual encryption occurred

        var decrypted = transformer.Restore(encrypted);
        decrypted.ShouldBe(original);
    }

    [Fact(DisplayName = "KyrolusAesCachePayloadTransformer: Static IV should work for deterministic encryption")]
    public void Aes_StaticIv_Roundtrip_Success()
    {
        var key = RandomNumberGenerator.GetBytes(16); // 128-bit key
        var iv = RandomNumberGenerator.GetBytes(16);  // 128-bit IV
        var transformer = new KyrolusAesCachePayloadTransformer(key, iv);
        var original = Encoding.UTF8.GetBytes("Secret with static IV");

        var encrypted = transformer.Transform(original);
        var decrypted = transformer.Restore(encrypted);
        decrypted.ShouldBe(original);
    }

    [Theory(DisplayName = "KyrolusAesCachePayloadTransformer: Invalid key sizes should throw ArgumentException")]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(64)]
    public void Aes_InvalidKeySize_ThrowsArgumentException(int invalidKeySize)
    {
        var key = new byte[invalidKeySize];
        Should.Throw<ArgumentException>(() => new KyrolusAesCachePayloadTransformer(key));
    }

    [Fact(DisplayName = "KyrolusAesCachePayloadTransformer: Null key should throw ArgumentNullException")]
    public void Aes_NullKey_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new KyrolusAesCachePayloadTransformer(null!));
    }

    [Fact(DisplayName = "KyrolusAesCachePayloadTransformer: Encrypted payload smaller than IV length should throw")]
    public void Aes_Restore_TooSmallPayload_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var transformer = new KyrolusAesCachePayloadTransformer(key);
        var corruptedSmall = new byte[8]; // Smaller than 16 bytes IV

        Should.Throw<InvalidOperationException>(() => transformer.Restore(corruptedSmall));
    }

    [Theory(DisplayName = "KyrolusAesCachePayloadTransformer: Invalid IV lengths != 16 should throw ArgumentException")]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(32)]
    public void Aes_InvalidIvLength_ThrowsArgumentException(int invalidIvLength)
    {
        var key = RandomNumberGenerator.GetBytes(16);
        var invalidIv = new byte[invalidIvLength];
        Should.Throw<ArgumentException>(() => new KyrolusAesCachePayloadTransformer(key, invalidIv));
    }
}
