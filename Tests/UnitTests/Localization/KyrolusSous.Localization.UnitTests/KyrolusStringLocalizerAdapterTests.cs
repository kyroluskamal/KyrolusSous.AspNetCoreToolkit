using KyrolusSous.Localization.StringLocalizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace KyrolusSous.Localization.UnitTests;

public class KyrolusStringLocalizerAdapterTests
{
    private sealed class TestStringLocalizer(IReadOnlyDictionary<string, string> translations) : IStringLocalizer
    {
        public LocalizedString this[string name]
        {
            get
            {
                var found = translations.TryGetValue(name, out var value);
                return new LocalizedString(name, value ?? name, !found);
            }
        }

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => translations.Select(t => new LocalizedString(t.Key, t.Value, false));
    }

    private sealed class TestTypedStringLocalizer<TResource>(IReadOnlyDictionary<string, string> translations) : IStringLocalizer<TResource>
    {
        public LocalizedString this[string name]
        {
            get
            {
                var found = translations.TryGetValue(name, out var value);
                return new LocalizedString(name, value ?? name, !found);
            }
        }

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => translations.Select(t => new LocalizedString(t.Key, t.Value, false));
    }

    private interface ITestResource;

    [Fact(DisplayName = "KyrolusStringLocalizerAdapter should return translated string and restore culture")]
    public void GetString_Should_Return_Translation_And_Restore_Culture()
    {
        var mockLocalizer = new TestStringLocalizer(new Dictionary<string, string>
        {
            ["validation_error"] = "Localized validation error"
        });
        var localizer = new KyrolusStringLocalizerAdapter(mockLocalizer);
        var targetCulture = new CultureInfo("fr-FR");
        var originalCulture = CultureInfo.CurrentUICulture;

        var result = localizer.GetString("validation_error", targetCulture);

        result.ResourceNotFound.ShouldBeFalse();
        result.Value.ShouldBe("Localized validation error");
        CultureInfo.CurrentUICulture.ShouldBe(originalCulture);
    }

    [Theory(DisplayName = "KyrolusStringLocalizerAdapter should report ResourceNotFound on missing or empty key")]
    [InlineData("not_found_key")]
    [InlineData("")]
    [InlineData(null)]
    public void GetString_Should_ReportResourceNotFound_When_Missing(string? key)
    {
        var mockLocalizer = new TestStringLocalizer(new Dictionary<string, string>());
        var localizer = new KyrolusStringLocalizerAdapter(mockLocalizer);

        var result = localizer.GetString(key!, culture: null);

        result.ResourceNotFound.ShouldBeTrue();
    }

    [Fact(DisplayName = "AddKyrolusStringLocalizerLocalization should register IKyrolusLocalizer backed by IStringLocalizer<TResource>")]
    public void AddKyrolusStringLocalizerLocalization_Should_Register_Localizer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStringLocalizer<ITestResource>>(new TestTypedStringLocalizer<ITestResource>(
            new Dictionary<string, string> { ["forbidden"] = "Accès refusé" }));

        services.AddKyrolusStringLocalizerLocalization<ITestResource>();

        var provider = services.BuildServiceProvider();
        var localizer = provider.GetService<IKyrolusLocalizer>();

        localizer.ShouldNotBeNull();
        localizer.ShouldBeOfType<KyrolusStringLocalizerAdapter>();
        localizer.GetString("forbidden").Value.ShouldBe("Accès refusé");
    }

    [Fact(DisplayName = "AddKyrolusStringLocalizerLocalization should also register a strongly-typed IKyrolusLocalizer<TResource>")]
    public void AddKyrolusStringLocalizerLocalization_Should_Register_TypedLocalizer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IStringLocalizer<ITestResource>>(new TestTypedStringLocalizer<ITestResource>(
            new Dictionary<string, string> { ["forbidden"] = "Accès refusé" }));

        services.AddKyrolusStringLocalizerLocalization<ITestResource>();

        var provider = services.BuildServiceProvider();
        var localizer = provider.GetService<IKyrolusLocalizer<ITestResource>>();

        localizer.ShouldNotBeNull();
        localizer.GetString("forbidden").Value.ShouldBe("Accès refusé");
    }

    [Fact(DisplayName = "KyrolusStringLocalizerAdapter.GetAllKeys returns every key from the underlying IStringLocalizer and restores the ambient culture")]
    public void GetAllKeys_ReturnsUnderlyingKeys_AndRestoresCulture()
    {
        var mockLocalizer = new TestStringLocalizer(new Dictionary<string, string>
        {
            ["a"] = "1",
            ["b"] = "2"
        });
        var localizer = new KyrolusStringLocalizerAdapter(mockLocalizer);
        var originalCulture = CultureInfo.CurrentUICulture;

        var keys = localizer.GetAllKeys(new CultureInfo("fr-FR")).ToList();

        keys.ShouldContain("a");
        keys.ShouldContain("b");
        CultureInfo.CurrentUICulture.ShouldBe(originalCulture);
    }
}
