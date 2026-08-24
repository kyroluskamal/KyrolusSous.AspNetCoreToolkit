namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusOrderedCachePayloadTransformerTests
{
    [Fact(DisplayName = "KyrolusOrderedCachePayloadTransformer: Should preserve order and delegate Transform and Restore")]
    public void OrderedTransformer_PreservesOrderAndDelegates()
    {
        var inner = new KyrolusBrotliCachePayloadTransformer();
        var ordered = new KyrolusOrderedCachePayloadTransformer(inner, 42);

        ordered.Order.ShouldBe(42);

        var data = Encoding.UTF8.GetBytes("Test data");
        var transformed = ordered.Transform(data);
        var restored = ordered.Restore(transformed);

        restored.ShouldBe(data);
    }

    [Fact(DisplayName = "KyrolusOrderedCachePayloadTransformer: Null inner transformer should throw ArgumentNullException")]
    public void NullInner_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new KyrolusOrderedCachePayloadTransformer(null!, 10));
    }
}
