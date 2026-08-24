using System.Security.Cryptography;

namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusTransformingCacheSerializerTests
{
    private sealed record UserProfile(int Id, string Username, string SecretToken);

    [Fact(DisplayName = "KyrolusTransformingCacheSerializer: Multi-step pipeline (JSON -> Brotli -> AES) should serialize forward and restore reverse")]
    public void FullPipeline_SerializeAndDeserialize_Roundtrip()
    {
        var baseSerializer = new KyrolusJsonCacheSerializer();
        var key = RandomNumberGenerator.GetBytes(32);

        // Order 10: Brotli Compression, Order 20: AES Encryption
        var brotli = new KyrolusOrderedCachePayloadTransformer(new KyrolusBrotliCachePayloadTransformer(minSizeBytes: 50), 10);
        var aes = new KyrolusOrderedCachePayloadTransformer(new KyrolusAesCachePayloadTransformer(key), 20);

        var pipeline = new KyrolusTransformingCacheSerializer(baseSerializer, [brotli, aes]);

        var user = new UserProfile(505, "kyrolus", "sk_live_very_secret_token_123456789");

        // Serialize: JSON -> Brotli -> AES
        var encryptedBytes = pipeline.Serialize(user);
        encryptedBytes.ShouldNotBeNull();

        // Deserialize: AES -> Brotli -> JSON
        var restoredUser = pipeline.Deserialize<UserProfile>(encryptedBytes);
        restoredUser.ShouldNotBeNull();
        restoredUser.Id.ShouldBe(505);
        restoredUser.Username.ShouldBe("kyrolus");
        restoredUser.SecretToken.ShouldBe("sk_live_very_secret_token_123456789");
    }

    [Fact(DisplayName = "KyrolusTransformingCacheSerializer: Null or empty payload in Deserialize should return default")]
    public void Deserialize_NullOrEmpty_ReturnsDefault()
    {
        var baseSerializer = new KyrolusJsonCacheSerializer();
        var pipeline = new KyrolusTransformingCacheSerializer(baseSerializer, []);

        pipeline.Deserialize<UserProfile>(null!).ShouldBeNull();
        pipeline.Deserialize<UserProfile>([]).ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusTransformingCacheSerializer: Null arguments in constructor should throw ArgumentNullException")]
    public void Constructor_NullArgs_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new KyrolusTransformingCacheSerializer(null!, []));
        Should.Throw<ArgumentNullException>(() => new KyrolusTransformingCacheSerializer(new KyrolusJsonCacheSerializer(), null!));
    }
}
