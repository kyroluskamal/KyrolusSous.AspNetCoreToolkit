namespace KyrolusSous.Caching.UnitTests.MessagePack;

public sealed class KyrolusMessagePackCacheSerializerTests
{
    public sealed class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = [];
    }

    [Fact(DisplayName = "KyrolusMessagePackCacheSerializer: Should serialize and deserialize POCO objects without contract annotations")]
    public void ContractlessPoco_Roundtrip_Success()
    {
        var serializer = new KyrolusMessagePackCacheSerializer();
        var user = new UserDto { Id = 10, Name = "Kyrolus Sous", Roles = ["Admin", "Developer"] };

        var bytes = serializer.Serialize(user);
        bytes.ShouldNotBeNull();
        bytes.Length.ShouldBeGreaterThan(0);

        var restored = serializer.Deserialize<UserDto>(bytes);
        restored.ShouldNotBeNull();
        restored.Id.ShouldBe(10);
        restored.Name.ShouldBe("Kyrolus Sous");
        restored.Roles.ShouldBe(["Admin", "Developer"]);
    }

    [Fact(DisplayName = "KyrolusMessagePackCacheSerializer: CreateWithLz4Compression MUST compress and physically reduce byte size on large payloads")]
    public void Lz4Compression_Compresses_And_ReducesSize()
    {
        var serializer = KyrolusMessagePackCacheSerializer.CreateWithLz4Compression();

        var list = new List<UserDto>();
        for (var i = 0; i < 300; i++)
        {
            list.Add(new UserDto
            {
                Id = i,
                Name = $"User Number {i} with long descriptive name for testing compression",
                Roles = ["Reader", "Writer", "Auditor", "Manager"]
            });
        }

        // Standard uncompressed serializer for comparison
        var standardSerializer = new KyrolusMessagePackCacheSerializer();
        var uncompressedBytes = standardSerializer.Serialize(list);

        // Compressed serializer
        var compressedBytes = serializer.Serialize(list);

        // Physical size reduction assertion
        compressedBytes.Length.ShouldBeLessThan(uncompressedBytes.Length);
        var ratio = (double)compressedBytes.Length / uncompressedBytes.Length;
        ratio.ShouldBeLessThan(0.45); // LZ4 should compress repetitive DTOs significantly

        // Roundtrip verification
        var restored = serializer.Deserialize<List<UserDto>>(compressedBytes);
        restored.ShouldNotBeNull();
        restored.Count.ShouldBe(300);
        restored[0].Name.ShouldBe(list[0].Name);
    }

    [Fact(DisplayName = "KyrolusMessagePackCacheSerializer: Serializing null returns empty array, and deserializing empty returns default")]
    public void NullAndEmptyHandling()
    {
        var serializer = new KyrolusMessagePackCacheSerializer();

        serializer.Serialize<UserDto>(null!).ShouldBeEmpty();
        serializer.Deserialize<UserDto>(null!).ShouldBeNull();
        serializer.Deserialize<UserDto>([]).ShouldBeNull();
    }
}
