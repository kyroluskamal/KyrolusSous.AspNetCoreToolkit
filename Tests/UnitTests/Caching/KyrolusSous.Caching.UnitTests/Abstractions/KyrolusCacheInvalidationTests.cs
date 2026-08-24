namespace KyrolusSous.Caching.UnitTests.Abstractions;

public sealed class KyrolusCacheInvalidationTests
{
    [Fact(DisplayName = "KyrolusCacheInvalidationMessage: Record properties and equality should work as expected")]
    public void InvalidationMessage_PropertiesAndEquality()
    {
        var values = new[] { "key1", "key2" };
        var message1 = new KyrolusCacheInvalidationMessage(KyrolusCacheInvalidationKind.Keys, values);
        var message2 = new KyrolusCacheInvalidationMessage(KyrolusCacheInvalidationKind.Keys, values);

        message1.Kind.ShouldBe(KyrolusCacheInvalidationKind.Keys);
        message1.Values.ShouldBe(values);
        message1.ShouldBe(message2);
    }

    [Fact(DisplayName = "KyrolusCacheInvalidationKind: Enums should have defined integer values")]
    public void InvalidationKind_Values()
    {
        ((int)KyrolusCacheInvalidationKind.Key).ShouldBe(1);
        ((int)KyrolusCacheInvalidationKind.Keys).ShouldBe(2);
        ((int)KyrolusCacheInvalidationKind.Tag).ShouldBe(3);
        ((int)KyrolusCacheInvalidationKind.Pattern).ShouldBe(4);
    }
}
