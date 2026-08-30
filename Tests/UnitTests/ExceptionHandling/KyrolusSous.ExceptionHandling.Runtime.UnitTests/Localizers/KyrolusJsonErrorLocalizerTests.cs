using System.Globalization;
using KyrolusSous.ExceptionHandling.Runtime.Localizers;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Localizers;

public class KyrolusJsonErrorLocalizerTests : IDisposable
{
    private readonly string _tempDirectory;

    public KyrolusJsonErrorLocalizerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "KyrolusJsonLocalizerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonErrorLocalizer loads all culture files from directory and resolves hierarchy")]
    public void Localizer_Should_Load_Directory_And_Resolve_Hierarchy()
    {
        // Arabic file
        File.WriteAllText(Path.Combine(_tempDirectory, "errors.ar.json"), """
        {
            "insufficient_funds": "رصيد غير كافٍ",
            "insufficient_funds.detail": "رصيد حسابك لا يكفي",
            "unauthorized": "غير مصرح"
        }
        """);

        // English file
        File.WriteAllText(Path.Combine(_tempDirectory, "errors.en.json"), """
        {
            "unauthorized": "Unauthorized access",
            "forbidden": "Forbidden access"
        }
        """);

        // Invariant default file
        File.WriteAllText(Path.Combine(_tempDirectory, "errors.json"), """
        {
            "general_error": "General Error Occurred"
        }
        """);

        var localizer = new KyrolusJsonErrorLocalizer(_tempDirectory);

        // Arabic test (ar-EG falls back to ar)
        var arTitle = localizer.Localize("insufficient_funds", "Default", new CultureInfo("ar-EG"));
        var arDetail = localizer.Localize("insufficient_funds.detail", "Default", new CultureInfo("ar"));
        arTitle.ShouldBe("رصيد غير كافٍ");
        arDetail.ShouldBe("رصيد حسابك لا يكفي");

        // English test (en-US falls back to en)
        var enTitle = localizer.Localize("unauthorized", "Default", new CultureInfo("en-US"));
        enTitle.ShouldBe("Unauthorized access");

        // Invariant fallback test
        var fallbackTitle = localizer.Localize("general_error", "Default", new CultureInfo("fr-FR"));
        fallbackTitle.ShouldBe("General Error Occurred");

        // Missing key returns default message
        var missing = localizer.Localize("non_existing_key", "Fallback", new CultureInfo("ar"));
        missing.ShouldBe("Fallback");
    }

    [Fact(DisplayName = "KyrolusJsonErrorLocalizer throws ArgumentException at startup when an invalid BCP-47 file name exists in directory")]
    public void Localizer_Should_Throw_FailFast_When_Invalid_FileName_In_Directory()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "errors.ar.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDirectory, "errors.arabic.json"), "{}");

        var ex = Should.Throw<ArgumentException>(() => new KyrolusJsonErrorLocalizer(_tempDirectory));
        ex.Message.ShouldContain("Must follow BCP-47 standard format");
    }

    [Fact(DisplayName = "KyrolusJsonErrorLocalizer throws DirectoryNotFoundException when directory does not exist")]
    public void Localizer_Should_Throw_When_Directory_Not_Found()
    {
        var nonExistentDir = Path.Combine(_tempDirectory, "does_not_exist_dir");
        Should.Throw<DirectoryNotFoundException>(() => new KyrolusJsonErrorLocalizer(nonExistentDir));
    }

    [Fact(DisplayName = "KyrolusJsonErrorLocalizer throws FileNotFoundException when directory has no json files")]
    public void Localizer_Should_Throw_When_Directory_Is_Empty()
    {
        Should.Throw<FileNotFoundException>(() => new KyrolusJsonErrorLocalizer(_tempDirectory));
    }

    [Fact(DisplayName = "AddKyrolusJsonErrorLocalizer registers directory localizer as singleton in DI")]
    public void AddKyrolusJsonErrorLocalizer_Should_Register_In_DI()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "errors.json"), "{}");

        var services = new ServiceCollection();
        services.AddKyrolusJsonErrorLocalizer(_tempDirectory);

        using var provider = services.BuildServiceProvider();
        var localizer = provider.GetService<IKyrolusErrorLocalizer>();

        localizer.ShouldNotBeNull();
        localizer.ShouldBeOfType<KyrolusJsonErrorLocalizer>();
    }
}
