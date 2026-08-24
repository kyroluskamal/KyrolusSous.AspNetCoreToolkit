namespace KyrolusSous.Caching.UnitTests.MessagePack;

public sealed class MessagePackServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "MessagePackExtensions: AddKyrolusMessagePackSerializer registers IKyrolusCacheSerializer in DI")]
    public void AddKyrolusMessagePackSerializer_RegistersInDi()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMessagePackSerializer();

        var provider = services.BuildServiceProvider();
        var serializer = provider.GetService<IKyrolusCacheSerializer>();

        serializer.ShouldNotBeNull();
        serializer.ShouldBeOfType<KyrolusMessagePackCacheSerializer>();
    }

    [Fact(DisplayName = "MessagePackExtensions: AddKyrolusMessagePackSerializerWithLz4 registers LZ4 compressed serializer")]
    public void AddKyrolusMessagePackSerializerWithLz4_RegistersInDi()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMessagePackSerializerWithLz4();

        var provider = services.BuildServiceProvider();
        var serializer = provider.GetService<IKyrolusCacheSerializer>();

        serializer.ShouldNotBeNull();
        serializer.ShouldBeOfType<KyrolusMessagePackCacheSerializer>();
    }
}
