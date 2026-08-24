namespace KyrolusSous.Caching.UnitTests.Abstractions;

[JsonSerializable(typeof(TestAotModel))]
internal partial class TestJsonContext : JsonSerializerContext
{
}

public sealed record TestAotModel(int Id, string Code);

public sealed class KyrolusJsonContextCacheSerializerTests
{
    [Fact(DisplayName = "KyrolusJsonContextCacheSerializer: Serializing registered type should succeed without reflection")]
    public void ContextSerializer_RegisteredType_Roundtrip()
    {
        var serializer = new KyrolusJsonContextCacheSerializer(TestJsonContext.Default);
        var model = new TestAotModel(42, "AOT_OK");

        var bytes = serializer.Serialize(model);
        bytes.ShouldNotBeNull();

        var restored = serializer.Deserialize<TestAotModel>(bytes);
        restored.ShouldNotBeNull();
        restored.Id.ShouldBe(42);
        restored.Code.ShouldBe("AOT_OK");
    }

    [Fact(DisplayName = "KyrolusJsonContextCacheSerializer: Serializing unregistered type should throw InvalidOperationException")]
    public void ContextSerializer_UnregisteredType_Throws()
    {
        var serializer = new KyrolusJsonContextCacheSerializer(TestJsonContext.Default);
        Should.Throw<InvalidOperationException>(() => serializer.Serialize(new { Unregistered = true }));
    }
}
