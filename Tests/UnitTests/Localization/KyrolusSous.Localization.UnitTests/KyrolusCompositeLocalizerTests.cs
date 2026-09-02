namespace KyrolusSous.Localization.UnitTests;

public class KyrolusCompositeLocalizerTests
{
    [Fact(DisplayName = "KyrolusCompositeLocalizer throws when constructed with no localizers")]
    public void Constructor_ThrowsWhenEmpty()
        => Should.Throw<ArgumentException>(() => new KyrolusCompositeLocalizer());

    [Fact(DisplayName = "KyrolusCompositeLocalizer resolves via the first localizer that has the key, in priority order")]
    public void GetString_PrefersEarlierLocalizer()
    {
        var overrides = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["GREETING"] = "Overridden hello" }
        });
        var defaults = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["GREETING"] = "Default hello", ["FAREWELL"] = "Bye" }
        });

        var composite = new KyrolusCompositeLocalizer(overrides, defaults);
        var culture = new CultureInfo("en-US");

        composite.GetString("GREETING", culture).Value.ShouldBe("Overridden hello");
        composite.GetString("FAREWELL", culture).Value.ShouldBe("Bye");
    }

    [Fact(DisplayName = "KyrolusCompositeLocalizer reports ResourceNotFound only when no source resolves the key")]
    public void GetString_ReportsNotFound_WhenNoSourceHasKey()
    {
        var a = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>());
        var b = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>());

        var composite = new KyrolusCompositeLocalizer(a, b);
        var result = composite.GetString("MISSING", new CultureInfo("en-US"));

        result.ResourceNotFound.ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusCompositeLocalizer formats template placeholders on the resolved value")]
    public void GetString_WithArguments_Formats()
    {
        var defaults = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["WELCOME"] = "Welcome, {Name}" }
        });

        var composite = new KyrolusCompositeLocalizer(defaults);
        var result = composite.GetString("WELCOME", new Dictionary<string, object?> { ["Name"] = "Kyrolus" }, new CultureInfo("en-US"));

        result.Value.ShouldBe("Welcome, Kyrolus");
    }

    [Fact(DisplayName = "KyrolusCompositeLocalizer.GetAllKeys unions keys across every source")]
    public void GetAllKeys_UnionsAcrossSources()
    {
        var a = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["GREETING"] = "Hi" }
        });
        var b = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["FAREWELL"] = "Bye" }
        });

        var composite = new KyrolusCompositeLocalizer(a, b);
        var keys = composite.GetAllKeys(new CultureInfo("en-US")).ToList();

        keys.ShouldContain("GREETING");
        keys.ShouldContain("FAREWELL");
    }

    [Fact(DisplayName = "KyrolusCompositeLocalizer.GetAvailableCultures unions culture names across every source")]
    public void GetAvailableCultures_UnionsAcrossSources()
    {
        var a = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ar"] = new Dictionary<string, string> { ["GREETING"] = "أهلاً" }
        });
        var b = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["GREETING"] = "Hi" }
        });

        var composite = new KyrolusCompositeLocalizer(a, b);
        var cultures = composite.GetAvailableCultures().ToList();

        cultures.ShouldContain("ar");
        cultures.ShouldContain("en");
    }
}
