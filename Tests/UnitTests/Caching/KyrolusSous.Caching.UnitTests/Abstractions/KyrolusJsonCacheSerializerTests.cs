namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusJsonCacheSerializerTests
{
    private sealed record CustomerModel(int Id, string Name, string Email);

    [Fact(DisplayName = "KyrolusJsonCacheSerializer: Should serialize and deserialize objects correctly")]
    public void SerializeAndDeserialize_Roundtrip()
    {
        var serializer = new KyrolusJsonCacheSerializer();
        var customer = new CustomerModel(101, "Kyrolus", "kyrolus@example.com");

        var bytes = serializer.Serialize(customer);
        bytes.ShouldNotBeNull();
        bytes.Length.ShouldBeGreaterThan(0);

        var restored = serializer.Deserialize<CustomerModel>(bytes);
        restored.ShouldNotBeNull();
        restored.Id.ShouldBe(101);
        restored.Name.ShouldBe("Kyrolus");
        restored.Email.ShouldBe("kyrolus@example.com");
    }

    [Fact(DisplayName = "KyrolusJsonCacheSerializer: Deserializing empty or null payload should return default")]
    public void Deserialize_NullOrEmpty_ReturnsDefault()
    {
        var serializer = new KyrolusJsonCacheSerializer();
        serializer.Deserialize<CustomerModel>([]).ShouldBeNull();
    }
}
