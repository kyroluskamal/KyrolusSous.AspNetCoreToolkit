namespace KyrolusSous.Localization.UnitTests;

public class KyrolusPluralRulesTests
{
    [Theory(DisplayName = "KyrolusPluralRules resolves English-like (one/other) categories")]
    [InlineData(0, KyrolusPluralCategory.Other)]
    [InlineData(1, KyrolusPluralCategory.One)]
    [InlineData(2, KyrolusPluralCategory.Other)]
    [InlineData(5, KyrolusPluralCategory.Other)]
    public void Resolve_English(long count, KyrolusPluralCategory expected)
        => KyrolusPluralRules.Resolve(new CultureInfo("en-US"), count).ShouldBe(expected);

    [Theory(DisplayName = "KyrolusPluralRules resolves all six Arabic CLDR categories")]
    [InlineData(0, KyrolusPluralCategory.Zero)]
    [InlineData(1, KyrolusPluralCategory.One)]
    [InlineData(2, KyrolusPluralCategory.Two)]
    [InlineData(3, KyrolusPluralCategory.Few)]
    [InlineData(10, KyrolusPluralCategory.Few)]
    [InlineData(11, KyrolusPluralCategory.Many)]
    [InlineData(99, KyrolusPluralCategory.Many)]
    [InlineData(100, KyrolusPluralCategory.Other)]
    [InlineData(103, KyrolusPluralCategory.Few)]
    public void Resolve_Arabic(long count, KyrolusPluralCategory expected)
        => KyrolusPluralRules.Resolve(new CultureInfo("ar-EG"), count).ShouldBe(expected);

    [Theory(DisplayName = "KyrolusPluralRules resolves Russian/Slavic categories")]
    [InlineData(1, KyrolusPluralCategory.One)]
    [InlineData(2, KyrolusPluralCategory.Few)]
    [InlineData(5, KyrolusPluralCategory.Many)]
    [InlineData(11, KyrolusPluralCategory.Many)]
    [InlineData(21, KyrolusPluralCategory.One)]
    public void Resolve_Russian(long count, KyrolusPluralCategory expected)
        => KyrolusPluralRules.Resolve(new CultureInfo("ru-RU"), count).ShouldBe(expected);

    [Fact(DisplayName = "KyrolusPluralRules resolves French's 0-and-1-are-one rule")]
    public void Resolve_French()
    {
        KyrolusPluralRules.Resolve(new CultureInfo("fr-FR"), 0).ShouldBe(KyrolusPluralCategory.One);
        KyrolusPluralRules.Resolve(new CultureInfo("fr-FR"), 1).ShouldBe(KyrolusPluralCategory.One);
        KyrolusPluralRules.Resolve(new CultureInfo("fr-FR"), 2).ShouldBe(KyrolusPluralCategory.Other);
    }

    [Theory(DisplayName = "KyrolusPluralRules resolves German, Swedish, and Spanish's \"only 1 is one\" rule")]
    [InlineData("de-DE", 0, KyrolusPluralCategory.Other)]
    [InlineData("de-DE", 1, KyrolusPluralCategory.One)]
    [InlineData("de-DE", 2, KyrolusPluralCategory.Other)]
    [InlineData("sv-SE", 1, KyrolusPluralCategory.One)]
    [InlineData("sv-SE", 2, KyrolusPluralCategory.Other)]
    [InlineData("es-ES", 1, KyrolusPluralCategory.One)]
    [InlineData("es-ES", 2, KyrolusPluralCategory.Other)]
    public void Resolve_German_Swedish_Spanish(string cultureName, long count, KyrolusPluralCategory expected)
        => KyrolusPluralRules.Resolve(new CultureInfo(cultureName), count).ShouldBe(expected);

    [Fact(DisplayName = "KyrolusPluralRules gives Brazilian Portuguese its own rule, distinct from European/generic Portuguese")]
    public void Resolve_Portuguese_DistinguishesBrazilianVariant()
    {
        // European/generic Portuguese: 0 and 1 both count as "one".
        KyrolusPluralRules.Resolve(new CultureInfo("pt-PT"), 0).ShouldBe(KyrolusPluralCategory.One);
        KyrolusPluralRules.Resolve(new CultureInfo("pt-PT"), 1).ShouldBe(KyrolusPluralCategory.One);
        KyrolusPluralRules.Resolve(new CultureInfo("pt-PT"), 2).ShouldBe(KyrolusPluralCategory.Other);

        // Brazilian Portuguese: only 1 counts as "one" - 0 is "other", unlike pt-PT.
        KyrolusPluralRules.Resolve(new CultureInfo("pt-BR"), 0).ShouldBe(KyrolusPluralCategory.Other);
        KyrolusPluralRules.Resolve(new CultureInfo("pt-BR"), 1).ShouldBe(KyrolusPluralCategory.One);
        KyrolusPluralRules.Resolve(new CultureInfo("pt-BR"), 2).ShouldBe(KyrolusPluralCategory.Other);
    }

    [Fact(DisplayName = "IKyrolusLocalizer.GetPlural picks the right Arabic category variant and injects {count}")]
    public void GetPlural_ResolvesArabicVariant_AndInjectsCount()
    {
        var cultureMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ar"] = new Dictionary<string, string>
            {
                ["Items.zero"] = "لا توجد عناصر",
                ["Items.one"] = "عنصر واحد",
                ["Items.two"] = "عنصران",
                ["Items.few"] = "{count} عناصر",
                ["Items.many"] = "{count} عنصرًا",
                ["Items.other"] = "{count} عنصر"
            }
        };
        IKyrolusLocalizer localizer = new KyrolusDictionaryLocalizer(cultureMap);
        var culture = new CultureInfo("ar-EG");

        localizer.GetPlural("Items", 0, culture: culture).Value.ShouldBe("لا توجد عناصر");
        localizer.GetPlural("Items", 1, culture: culture).Value.ShouldBe("عنصر واحد");
        localizer.GetPlural("Items", 2, culture: culture).Value.ShouldBe("عنصران");
        localizer.GetPlural("Items", 5, culture: culture).Value.ShouldBe("5 عناصر");
        localizer.GetPlural("Items", 20, culture: culture).Value.ShouldBe("20 عنصرًا");
    }

    [Fact(DisplayName = "IKyrolusLocalizer.GetPlural falls back to \".other\" and then the plain key when a category-specific variant is missing")]
    public void GetPlural_FallsBackWhenCategoryVariantMissing()
    {
        var cultureMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["Items.other"] = "{count} items" }
        };
        IKyrolusLocalizer localizer = new KyrolusDictionaryLocalizer(cultureMap);

        // count=1 resolves to category "one", which has no "Items.one" entry, so this must fall back to "Items.other".
        var result = localizer.GetPlural("Items", 1, culture: new CultureInfo("en-US"));

        result.Value.ShouldBe("1 items");
    }

    [Fact(DisplayName = "KyrolusPluralRules.Resolve does not throw for long.MinValue")]
    public void Resolve_DoesNotThrow_ForLongMinValue()
    {
        Should.NotThrow(() => KyrolusPluralRules.Resolve(new CultureInfo("ar-EG"), long.MinValue));
        Should.NotThrow(() => KyrolusPluralRules.Resolve(new CultureInfo("en-US"), long.MinValue));
    }

    [Fact(DisplayName = "KyrolusPluralRules.Register lets a consumer plug in a language this library doesn't ship a rule for")]
    public void Register_AddsSupportForAnArbitraryLanguage()
    {
        var polish = new CultureInfo("pl-PL");

        // Before registering, an uncovered language falls back to the English-like one/other default.
        KyrolusPluralRules.Resolve(polish, 2).ShouldBe(KyrolusPluralCategory.Other);

        KyrolusPluralRules.Register("pl", n =>
        {
            if (n == 1) return KyrolusPluralCategory.One;
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14) return KyrolusPluralCategory.Few;
            return KyrolusPluralCategory.Many;
        });

        KyrolusPluralRules.Resolve(polish, 1).ShouldBe(KyrolusPluralCategory.One);
        KyrolusPluralRules.Resolve(polish, 2).ShouldBe(KyrolusPluralCategory.Few);
        KyrolusPluralRules.Resolve(polish, 5).ShouldBe(KyrolusPluralCategory.Many);
    }

    [Fact(DisplayName = "KyrolusPluralRules.Register under a full culture name overrides the language-level rule for just that culture")]
    public void Register_ByCultureName_TakesPrecedenceOverLanguageRule()
    {
        // KyrolusPluralRules.Rules is shared, static, mutable state for the whole test process - overriding
        // "ar-EG" here would leak into every other test in this file that resolves/uses "ar-EG" (Resolve_Arabic,
        // GetPlural_ResolvesArabicVariant_AndInjectsCount), regardless of xUnit's (unspecified) method execution
        // order. "ar-SA" is not used by any other test in this file, so overriding it is fully isolated; "ar-EG"
        // is the untouched control proving the built-in Arabic rule still applies elsewhere.
        KyrolusPluralRules.Register("ar-SA", _ => KyrolusPluralCategory.Other);

        KyrolusPluralRules.Resolve(new CultureInfo("ar-SA"), 2).ShouldBe(KyrolusPluralCategory.Other);
        KyrolusPluralRules.Resolve(new CultureInfo("ar-EG"), 2).ShouldBe(KyrolusPluralCategory.Two);
    }

    [Fact(DisplayName = "IKyrolusLocalizer.GetPlural does not overwrite an explicitly-supplied \"count\" argument")]
    public void GetPlural_DoesNotOverwriteExplicitCountArgument()
    {
        var cultureMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["Items.other"] = "{count} of {total}" }
        };
        IKyrolusLocalizer localizer = new KyrolusDictionaryLocalizer(cultureMap);

        var result = localizer.GetPlural("Items", 3,
            new Dictionary<string, object?> { ["count"] = "three", ["total"] = 10 },
            new CultureInfo("en-US"));

        result.Value.ShouldBe("three of 10");
    }

    [Fact(DisplayName = "GetPluralOrDefault returns the resolved value when found, and the default when not found or the localizer is null")]
    public void GetPluralOrDefault_ReturnsResolvedOrDefault()
    {
        IKyrolusLocalizer localizer = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["Items.other"] = "{count} items" }
        });
        var culture = new CultureInfo("en-US");

        localizer.GetPluralOrDefault("Items", 3, "fallback", culture: culture).ShouldBe("3 items");
        localizer.GetPluralOrDefault("Missing", 3, "fallback", culture: culture).ShouldBe("fallback");

        IKyrolusLocalizer? nullLocalizer = null;
        nullLocalizer.GetPluralOrDefault("Items", 3, "fallback").ShouldBe("fallback");
    }
}
