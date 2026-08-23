namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Localizers;

public class KyrolusErrorLocalizersTests
{
    [Fact(DisplayName = "KyrolusNullErrorLocalizer should return default message unchanged")]
    public void KyrolusNullErrorLocalizer_Should_Return_Default_Message()
    {
        var localizer = new KyrolusNullErrorLocalizer();

        var result = localizer.Localize("error_code", "Default message", CultureInfo.InvariantCulture);

        result.ShouldBe("Default message");
    }

    [Fact(DisplayName = "KyrolusDictionaryErrorLocalizer should return translation when key exists")]
    public void KyrolusDictionaryErrorLocalizer_Should_Return_Translation_When_Key_Exists()
    {
        var dictionary = new Dictionary<string, string>
        {
            ["item_not_found"] = "Translated item not found"
        };
        var localizer = new KyrolusDictionaryErrorLocalizer(dictionary);

        var result = localizer.Localize("item_not_found", "Default item not found", null);

        result.ShouldBe("Translated item not found");
    }

    [Theory(DisplayName = "KyrolusDictionaryErrorLocalizer should fallback to default message when key is missing or blank")]
    [InlineData("missing_key", "Default missing")]
    [InlineData("", "Default blank")]
    [InlineData("   ", "Default whitespace")]
    [InlineData(null, "Default null")]
    public void KyrolusDictionaryErrorLocalizer_Should_Fallback_To_Default(string? code, string? defaultMessage)
    {
        var dictionary = new Dictionary<string, string>
        {
            ["existing_key"] = "Existing translation"
        };
        var localizer = new KyrolusDictionaryErrorLocalizer(dictionary);

        var result = localizer.Localize(code!, defaultMessage, null);

        result.ShouldBe(defaultMessage);
    }

    [Fact(DisplayName = "KyrolusStringLocalizerErrorLocalizer should return translated string and restore culture")]
    public void KyrolusStringLocalizerErrorLocalizer_Should_Return_Translation_And_Restore_Culture()
    {
        var mockLocalizer = new TestStringLocalizer(new Dictionary<string, string>
        {
            ["validation_error"] = "Localized validation error"
        });
        var localizer = new KyrolusStringLocalizerErrorLocalizer(mockLocalizer);
        var targetCulture = new CultureInfo("fr-FR");
        var originalCulture = CultureInfo.CurrentUICulture;

        var result = localizer.Localize("validation_error", "Default message", targetCulture);

        result.ShouldBe("Localized validation error");
        CultureInfo.CurrentUICulture.ShouldBe(originalCulture);
    }

    [Theory(DisplayName = "KyrolusStringLocalizerErrorLocalizer should fallback to default message on missing or empty key")]
    [InlineData("not_found_key", "Default not found")]
    [InlineData("", "Default blank")]
    [InlineData("   ", "Default whitespace")]
    [InlineData(null, "Default null")]
    public void KyrolusStringLocalizerErrorLocalizer_Should_Fallback_To_Default(string? code, string? defaultMessage)
    {
        var mockLocalizer = new TestStringLocalizer(new Dictionary<string, string>());
        var localizer = new KyrolusStringLocalizerErrorLocalizer(mockLocalizer);

        var result = localizer.Localize(code!, defaultMessage, null);

        result.ShouldBe(defaultMessage);
    }
}
