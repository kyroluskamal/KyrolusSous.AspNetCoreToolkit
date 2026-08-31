namespace KyrolusSous.Localization.UnitTests;

public class KyrolusJsonLocalizerTests
{
    [Fact(DisplayName = "KyrolusJsonLocalizer strictly enforces category and filters out non-matching files")]
    public void KyrolusJsonLocalizer_EnforcesCategory_And_FiltersOutOthers()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create validation files and error files in the same directory
            File.WriteAllText(Path.Combine(tempDir, "validation.ar.json"), "{\"ERR_REQ\":\"حقل مطلوب\",\"ERR_DUP\":\"قيمة مكررة\"}");
            File.WriteAllText(Path.Combine(tempDir, "validation.en.json"), "{\"ERR_REQ\":\"Required field\",\"ERR_DUP\":\"Duplicate value\"}");
            File.WriteAllText(Path.Combine(tempDir, "errors.ar.json"), "{\"ERR_AUTH\":\"غير مصرح لك\"}");
            File.WriteAllText(Path.Combine(tempDir, "errors.en.json"), "{\"ERR_AUTH\":\"Unauthorized access\"}");

            // Localizer with RequiredCategory = "validation"
            var validationLocalizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                RequiredCategory = "validation"
            });

            // Localizer with RequiredCategory = "errors"
            var errorsLocalizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                RequiredCategory = "errors"
            });

            // 1. Validation localizer finds validation keys
            var valResultAr = validationLocalizer.GetString("ERR_REQ", new CultureInfo("ar-EG"));
            valResultAr.ResourceNotFound.ShouldBeFalse();
            valResultAr.Value.ShouldBe("حقل مطلوب");

            // 2. Validation localizer does NOT have error keys (filtered out)
            var valResultAuth = validationLocalizer.GetString("ERR_AUTH", new CultureInfo("ar-EG"));
            valResultAuth.ResourceNotFound.ShouldBeTrue();

            // 3. Error localizer finds error keys
            var errResultAr = errorsLocalizer.GetString("ERR_AUTH", new CultureInfo("ar-EG"));
            errResultAr.ResourceNotFound.ShouldBeFalse();
            errResultAr.Value.ShouldBe("غير مصرح لك");

            // 4. Error localizer does NOT have validation keys
            var errResultReq = errorsLocalizer.GetString("ERR_REQ", new CultureInfo("ar-EG"));
            errResultReq.ResourceNotFound.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer throws FileNotFoundException when required category files are absent")]
    public void KyrolusJsonLocalizer_ThrowsWhenCategoryNotFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocMissingTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "errors.ar.json"), "{\"ERR_AUTH\":\"غير مصرح لك\"}");

            Should.Throw<FileNotFoundException>(() => new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                RequiredCategory = "validation"
            }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer strictly validates BCP-47 tags at startup")]
    public void KyrolusJsonLocalizer_ValidatesBcp47Tags()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocBcpTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "validation.invalid_culture_tag_12345.json"), "{\"KEY\":\"Value\"}");

            Should.Throw<ArgumentException>(() => new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                StrictBcp47Validation = true
            }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer formats template parameters using dictionaries and POCOs")]
    public void KyrolusJsonLocalizer_FormatsTemplatePlaceholders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocFormatTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "validation.en.json"), "{\"ERR_MIN\":\"Field {PropertyName} must have at least {Min} chars, got '{AttemptedValue}'\"}");

            var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir
            });

            // Dictionary arguments
            var dictArgs = new Dictionary<string, object?>
            {
                ["PropertyName"] = "Username",
                ["Min"] = 5,
                ["AttemptedValue"] = "abc"
            };

            var res = localizer.GetString("ERR_MIN", dictArgs, new CultureInfo("en-US"));
            res.ResourceNotFound.ShouldBeFalse();
            res.Value.ShouldBe("Field Username must have at least 5 chars, got 'abc'");

            // POCO arguments
            var pocoArgs = new { PropertyName = "Password", Min = 8, AttemptedValue = "123" };
            var resPoco = localizer.GetString("ERR_MIN", pocoArgs, new CultureInfo("en-US"));
            resPoco.Value.ShouldBe("Field Password must have at least 8 chars, got '123'");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer treats an uncategorized, undotted file as invariant instead of throwing")]
    public void KyrolusJsonLocalizer_TreatsPlainFileNameAsInvariant()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocPlainTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            // No culture segment and no RequiredCategory configured - "messages" is not a BCP-47 tag,
            // so this must load as the invariant bucket rather than throw ArgumentException.
            File.WriteAllText(Path.Combine(tempDir, "messages.json"), "{\"GREETING\":\"Hello\"}");

            var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir
            });

            var result = localizer.GetString("GREETING");
            result.ResourceNotFound.ShouldBeFalse();
            result.Value.ShouldBe("Hello");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusDictionaryLocalizer resolves and formats correctly")]
    public void KyrolusDictionaryLocalizer_ResolvesAndFormats()
    {
        var cultureMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ar-EG"] = new Dictionary<string, string>
            {
                ["ERR_MAX"] = "الحقل {PropertyName} تجاوز الحد الأقصى {Max}"
            }
        };

        var localizer = new KyrolusDictionaryLocalizer(cultureMap);
        var res = localizer.GetString("ERR_MAX", new { PropertyName = "العنوان", Max = 100 }, new CultureInfo("ar-EG"));

        res.ResourceNotFound.ShouldBeFalse();
        res.Value.ShouldBe("الحقل العنوان تجاوز الحد الأقصى 100");
    }
}
