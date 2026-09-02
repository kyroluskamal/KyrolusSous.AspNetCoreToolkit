namespace KyrolusSous.Localization.UnitTests;

public class KyrolusMissingTranslationTrackingLocalizerTests
{
    private static KyrolusDictionaryLocalizer BuildInner() => new(new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en"] = new Dictionary<string, string> { ["GREETING"] = "Hello" }
    });

    [Fact(DisplayName = "KyrolusMissingTranslationTrackingLocalizer invokes the callback only when a lookup is not found")]
    public void GetString_InvokesCallback_OnlyWhenMissing()
    {
        var missed = new List<(string Key, string Culture)>();
        var localizer = new KyrolusMissingTranslationTrackingLocalizer(
            BuildInner(),
            (key, culture) => missed.Add((key, culture.Name)));
        var culture = new CultureInfo("en-US");

        var found = localizer.GetString("GREETING", culture);
        var notFound = localizer.GetString("MISSING", culture);

        found.ResourceNotFound.ShouldBeFalse();
        notFound.ResourceNotFound.ShouldBeTrue();
        missed.Count.ShouldBe(1);
        missed[0].Key.ShouldBe("MISSING");
        missed[0].Culture.ShouldBe("en-US");
    }

    [Fact(DisplayName = "KyrolusMissingTranslationTrackingLocalizer invokes the callback for the GetString(arguments) overload too")]
    public void GetStringWithArguments_InvokesCallback_WhenMissing()
    {
        var missCount = 0;
        var localizer = new KyrolusMissingTranslationTrackingLocalizer(BuildInner(), (_, _) => missCount++);

        localizer.GetString("MISSING", new Dictionary<string, object?> { ["X"] = 1 }, new CultureInfo("en-US"));

        missCount.ShouldBe(1);
    }

    [Fact(DisplayName = "KyrolusMissingTranslationTrackingLocalizer delegates Format and GetAllKeys to the wrapped localizer")]
    public void Format_And_GetAllKeys_Delegate()
    {
        var localizer = new KyrolusMissingTranslationTrackingLocalizer(BuildInner(), (_, _) => { });

        localizer.Format("Hi {Name}", new Dictionary<string, object?> { ["Name"] = "Kyrolus" }).ShouldBe("Hi Kyrolus");
        localizer.GetAllKeys(new CultureInfo("en-US")).ShouldContain("GREETING");
    }

    [Fact(DisplayName = "KyrolusMissingTranslationTrackingLocalizer throws ArgumentNullException for null inner/handler")]
    public void Constructor_ThrowsOnNullArguments()
    {
        Should.Throw<ArgumentNullException>(() => new KyrolusMissingTranslationTrackingLocalizer(null!, (_, _) => { }));
        Should.Throw<ArgumentNullException>(() => new KyrolusMissingTranslationTrackingLocalizer(BuildInner(), null!));
    }
}
