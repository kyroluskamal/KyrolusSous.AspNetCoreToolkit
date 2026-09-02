using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact(DisplayName = "KyrolusJsonLocalizer formats template parameters using dictionaries and KeyValuePair sequences")]
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

            // KeyValuePair sequence arguments (reflection-free alternative to an anonymous object)
            KeyValuePair<string, object?>[] namedArgs =
            [
                new("PropertyName", "Password"),
                new("Min", 8),
                new("AttemptedValue", "123")
            ];
            var resNamed = localizer.GetString("ERR_MIN", namedArgs, new CultureInfo("en-US"));
            resNamed.Value.ShouldBe("Field Password must have at least 8 chars, got '123'");
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
        var res = localizer.GetString(
            "ERR_MAX",
            new Dictionary<string, object?> { ["PropertyName"] = "العنوان", ["Max"] = 100 },
            new CultureInfo("ar-EG"));

        res.ResourceNotFound.ShouldBeFalse();
        res.Value.ShouldBe("الحقل العنوان تجاوز الحد الأقصى 100");
    }

    [Fact(DisplayName = "KyrolusLocalizationFormatter rejects POCO/anonymous arguments instead of reading them via reflection")]
    public void KyrolusLocalizationFormatter_RejectsArbitraryObjects()
    {
        var ex = Should.Throw<NotSupportedException>(() =>
            KyrolusLocalizationFormatter.Format("Field {PropertyName}", new { PropertyName = "Username" }));

        ex.Message.ShouldContain("IDictionary<string, object?>");
    }

    [Fact(DisplayName = "KyrolusLocalizationFormatter does not re-expand a value that looks like another placeholder (no second-order injection)")]
    public void KyrolusLocalizationFormatter_DoesNotReExpandSubstitutedValues()
    {
        // AttemptedValue is attacker/user-controlled in real usage; if it literally contains "{PropertyName}",
        // a naive sequential-replace implementation would let PropertyName's own value bleed into where the
        // user's raw text should have been rendered untouched.
        var args = new Dictionary<string, object?>
        {
            ["AttemptedValue"] = "{PropertyName}",
            ["PropertyName"] = "Email"
        };

        var result = KyrolusLocalizationFormatter.Format("Value '{AttemptedValue}' is invalid for {PropertyName}", args);

        result.ShouldBe("Value '{PropertyName}' is invalid for Email");
    }

    [Fact(DisplayName = "KyrolusLocalizationFormatter leaves an unknown placeholder untouched")]
    public void KyrolusLocalizationFormatter_LeavesUnknownPlaceholderUntouched()
    {
        var result = KyrolusLocalizationFormatter.Format("Hello {Name}, you have {Unknown} messages", new Dictionary<string, object?> { ["Name"] = "Kyrolus" });

        result.ShouldBe("Hello Kyrolus, you have {Unknown} messages");
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer honors FallbackCulture when the requested culture's own hierarchy has no translation")]
    public void KyrolusJsonLocalizer_HonorsFallbackCulture()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocFallbackTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "messages.en.json"), "{\"GREETING\":\"Hello\"}");

            var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                FallbackCulture = "en"
            });

            // "fr-FR" has no translations at all; only the configured FallbackCulture ("en") does.
            var result = localizer.GetString("GREETING", new CultureInfo("fr-FR"));

            result.ResourceNotFound.ShouldBeFalse();
            result.Value.ShouldBe("Hello");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusDictionaryLocalizer falls back through the culture hierarchy (e.g. ar-EG falls back to ar)")]
    public void KyrolusDictionaryLocalizer_FallsBackThroughCultureHierarchy()
    {
        var cultureMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ar"] = new Dictionary<string, string> { ["GREETING"] = "أهلاً" }
        };

        var localizer = new KyrolusDictionaryLocalizer(cultureMap);

        // No "ar-EG" entry exists - must fall back to the parent "ar" entry instead of failing outright.
        var result = localizer.GetString("GREETING", new CultureInfo("ar-EG"));

        result.ResourceNotFound.ShouldBeFalse();
        result.Value.ShouldBe("أهلاً");
        result.SearchedLocation.ShouldBe("ar");
    }

    [Fact(DisplayName = "KyrolusDictionaryLocalizer honors an explicit fallbackCulture before the invariant map")]
    public void KyrolusDictionaryLocalizer_HonorsFallbackCulture()
    {
        var cultureMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["GREETING"] = "Hello" }
        };

        var localizer = new KyrolusDictionaryLocalizer(cultureMap, fallbackCulture: "en");

        var result = localizer.GetString("GREETING", new CultureInfo("fr-FR"));

        result.ResourceNotFound.ShouldBeFalse();
        result.Value.ShouldBe("Hello");
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer flattens nested JSON objects into dot-separated keys")]
    public void KyrolusJsonLocalizer_FlattensNestedJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocNestedTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "messages.en.json"),
                """{"Errors":{"Required":"This field is required.","Nested":{"TooLong":"Too long."}},"Flat":"Still works"}""");

            var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions { DirectoryPath = tempDir });

            localizer.GetString("Errors.Required", new CultureInfo("en-US")).Value.ShouldBe("This field is required.");
            localizer.GetString("Errors.Nested.TooLong", new CultureInfo("en-US")).Value.ShouldBe("Too long.");
            localizer.GetString("Flat", new CultureInfo("en-US")).Value.ShouldBe("Still works");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer throws on a literal duplicate key within the same file regardless of ThrowOnDuplicateKeys")]
    public void KyrolusJsonLocalizer_ThrowsOnDuplicateKeyWithinSameFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocDupSameFileTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "messages.en.json"), """{"A":"1","A":"2"}""");

            Should.Throw<ArgumentException>(() => new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions { DirectoryPath = tempDir }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer throws on a cross-file duplicate key for the same culture only when ThrowOnDuplicateKeys is set")]
    public void KyrolusJsonLocalizer_ThrowsOnCrossFileDuplicateKey_WhenConfigured()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocDupCrossFileTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "defaults.en.json"), "{\"GREETING\":\"Hello\"}");
            File.WriteAllText(Path.Combine(tempDir, "overrides.en.json"), "{\"GREETING\":\"Hi\"}");

            // Default: last-file-wins, no throw.
            var lenient = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions { DirectoryPath = tempDir });
            lenient.GetString("GREETING", new CultureInfo("en-US")).ResourceNotFound.ShouldBeFalse();

            // Opt-in: fail fast instead of silently letting the second file win.
            Should.Throw<InvalidOperationException>(() => new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                ThrowOnDuplicateKeys = true
            }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer hot-reloads translations when EnableHotReload is set and a file changes")]
    public async Task KyrolusJsonLocalizer_HotReloadsOnFileChange()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocHotReloadTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "messages.en.json");
        File.WriteAllText(filePath, "{\"GREETING\":\"Hello\"}");

        try
        {
            using var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                EnableHotReload = true
            });

            localizer.GetString("GREETING", new CultureInfo("en-US")).Value.ShouldBe("Hello");

            File.WriteAllText(filePath, "{\"GREETING\":\"Hi there\"}");

            var deadline = DateTime.UtcNow.AddSeconds(5);
            string? latest = null;
            while (DateTime.UtcNow < deadline)
            {
                latest = localizer.GetString("GREETING", new CultureInfo("en-US")).Value;
                if (latest == "Hi there") break;
                await Task.Delay(100);
            }

            latest.ShouldBe("Hi there");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "AddKyrolusJsonLocalization<TCategory> registers a strongly-typed IKyrolusLocalizer<TCategory>")]
    public void AddKyrolusJsonLocalization_Generic_RegistersTypedLocalizer()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocGenericDiTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "validationmessages.en.json"), "{\"REQUIRED\":\"Required\"}");

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddKyrolusJsonLocalization<ValidationMessages>(opt => opt.DirectoryPath = tempDir);

            var provider = services.BuildServiceProvider();
            var localizer = provider.GetService<IKyrolusLocalizer<ValidationMessages>>();

            localizer.ShouldNotBeNull();
            localizer.GetString("REQUIRED", new CultureInfo("en-US")).Value.ShouldBe("Required");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusLocalizationFormatter still substitutes a later well-formed placeholder even when an earlier stray '{' appears in the template")]
    public void KyrolusLocalizationFormatter_HandlesStrayBraceBeforeValidPlaceholder()
    {
        var args = new Dictionary<string, object?> { ["RealKey"] = "resolved" };

        var result = KyrolusLocalizationFormatter.Format("Use {SomeText not a key {RealKey} works", args);

        result.ShouldBe("Use {SomeText not a key resolved works");
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer throws a clear error when a file's top-level JSON value isn't an object")]
    public void KyrolusJsonLocalizer_ThrowsWhenRootIsNotAnObject()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocNotObjectTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "messages.en.json"), "\"just a string\"");

            Should.Throw<JsonException>(() => new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions { DirectoryPath = tempDir }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer keeps last-good translations and does not throw when a hot-reload hits a malformed file (e.g. a duplicate key)")]
    public async Task KyrolusJsonLocalizer_SurvivesMalformedHotReload()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocBadReloadTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "messages.en.json");
        File.WriteAllText(filePath, "{\"GREETING\":\"Hello\"}");

        try
        {
            using var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                EnableHotReload = true
            });

            localizer.GetString("GREETING", new CultureInfo("en-US")).Value.ShouldBe("Hello");

            // A literal duplicate key within one file's own JSON always throws while loading. If the watcher's
            // background callback ever let that escape uncaught, it would crash the whole process rather than
            // just skip the reload - this must not happen, and GetString must keep returning the last-good value.
            File.WriteAllText(filePath, "{\"GREETING\":\"Hi\",\"GREETING\":\"Hola\"}");

            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                localizer.GetString("GREETING", new CultureInfo("en-US")).Value.ShouldBe("Hello");
                await Task.Delay(100);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer.GetAllKeys enumerates keys across the culture fallback chain")]
    public void KyrolusJsonLocalizer_GetAllKeys_EnumeratesAcrossFallbackChain()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocGetAllKeysTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "messages.ar.json"), "{\"A\":\"1\"}");
            File.WriteAllText(Path.Combine(tempDir, "messages.ar-EG.json"), "{\"B\":\"2\"}");

            var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions { DirectoryPath = tempDir });
            var keys = localizer.GetAllKeys(new CultureInfo("ar-EG")).ToList();

            keys.ShouldContain("A");
            keys.ShouldContain("B");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusDictionaryLocalizer.GetAllKeys enumerates culture-chain keys plus the invariant map")]
    public void KyrolusDictionaryLocalizer_GetAllKeys_EnumeratesEverything()
    {
        var localizer = new KyrolusDictionaryLocalizer(
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["ar"] = new Dictionary<string, string> { ["A"] = "1" }
            },
            invariantMap: new Dictionary<string, string> { ["C"] = "3" });

        var keys = localizer.GetAllKeys(new CultureInfo("ar-EG")).ToList();

        keys.ShouldContain("A");
        keys.ShouldContain("C");
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer walks a multi-level FallbackCultures chain in order")]
    public void KyrolusJsonLocalizer_WalksMultiLevelFallbackChain()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocMultiFallbackTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Only "fr" has ONLY_IN_FR, only "en" has ONLY_IN_EN. Requested culture "de-DE" has neither on its
            // own, and FallbackCulture ("fr") doesn't have ONLY_IN_EN either - only the second-level
            // FallbackCultures entry ("en") does, proving the chain is walked past the first fallback.
            File.WriteAllText(Path.Combine(tempDir, "messages.fr.json"), "{\"ONLY_IN_FR\":\"bonjour\"}");
            File.WriteAllText(Path.Combine(tempDir, "messages.en.json"), "{\"ONLY_IN_EN\":\"hello\"}");

            var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions
            {
                DirectoryPath = tempDir,
                FallbackCulture = "fr",
                FallbackCultures = ["en"]
            });

            var culture = new CultureInfo("de-DE");
            localizer.GetString("ONLY_IN_FR", culture).ResourceNotFound.ShouldBeFalse();
            localizer.GetString("ONLY_IN_EN", culture).ResourceNotFound.ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusJsonLocalizer.GetAvailableCultures lists every real culture with translations, excluding the invariant bucket")]
    public void KyrolusJsonLocalizer_GetAvailableCultures_ListsRealCulturesOnly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KyrolusLocAvailableCulturesTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "messages.ar.json"), "{\"A\":\"1\"}");
            File.WriteAllText(Path.Combine(tempDir, "messages.en.json"), "{\"A\":\"1\"}");
            File.WriteAllText(Path.Combine(tempDir, "messages.json"), "{\"A\":\"1\"}"); // invariant bucket

            var localizer = new KyrolusJsonLocalizer(new KyrolusJsonLocalizationOptions { DirectoryPath = tempDir });
            var cultures = localizer.GetAvailableCultures().ToList();

            cultures.ShouldContain("ar");
            cultures.ShouldContain("en");
            cultures.ShouldNotContain(string.Empty);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact(DisplayName = "KyrolusDictionaryLocalizer.GetAvailableCultures lists the configured culture keys")]
    public void KyrolusDictionaryLocalizer_GetAvailableCultures_ListsConfiguredCultures()
    {
        var localizer = new KyrolusDictionaryLocalizer(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ar"] = new Dictionary<string, string> { ["A"] = "1" },
            ["en"] = new Dictionary<string, string> { ["A"] = "1" }
        });

        var cultures = localizer.GetAvailableCultures().ToList();

        cultures.ShouldContain("ar");
        cultures.ShouldContain("en");
    }

    private sealed class ValidationMessages;
}
