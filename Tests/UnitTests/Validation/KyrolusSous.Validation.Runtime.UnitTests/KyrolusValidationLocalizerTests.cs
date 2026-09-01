namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationLocalizerTests
{
    private sealed class TestDictionaryLocalizer(IReadOnlyDictionary<string, string> translations) : IKyrolusLocalizer
    {
        public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null) =>
            translations.TryGetValue(key, out var value)
                ? new KyrolusLocalizationResult(value, ResourceNotFound: false)
                : new KyrolusLocalizationResult(key, ResourceNotFound: true);

        public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
        {
            var result = GetString(key, culture);
            if (result.ResourceNotFound || arguments is null) return result;
            return result with { Value = Format(result.Value, arguments) };
        }

        public string Format(string template, object? arguments) => KyrolusLocalizationFormatter.Format(template, arguments);
    }

    private sealed record ProbeRequest;

    // Assembly-scanning tests elsewhere in this project (AddKyrolusScannedValidators) reflect
    // over every IKyrolusRequestValidator<> in the assembly and construct it via DI, so this needs a
    // DI-resolvable (default-valued) constructor even though our own tests always pass failures explicitly.
    private sealed class ProbeValidator(IReadOnlyList<KyrolusValidationFailure>? failures = null) : IKyrolusRequestValidator<ProbeRequest>
    {
        public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(ProbeRequest request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(failures ?? []);
    }

    private static async Task<KyrolusValidationFailure> RunSingleFailureAsync(
        KyrolusValidationFailure failure,
        IKyrolusLocalizer localizer)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusRequestValidator<ProbeRequest>>(new ProbeValidator([failure]));
        using var serviceProvider = services.BuildServiceProvider();

        var engine = new KyrolusValidationEngine(serviceProvider, localizer);
        var result = await engine.ValidateAsync(new ProbeRequest());

        return result[0];
    }

    [Fact(DisplayName = "Localization prefers MessageKey over ErrorCode and ErrorMessage")]
    public async Task Localize_Prefers_MessageKey()
    {
        var localizer = new TestDictionaryLocalizer(new Dictionary<string, string>
        {
            ["by.key"] = "Localized by key",
            ["by.code"] = "Localized by code"
        });

        var failure = new KyrolusValidationFailure("Name", "original message", ErrorCode: "by.code", MessageKey: "by.key");
        var result = await RunSingleFailureAsync(failure, localizer);

        result.ErrorMessage.ShouldBe("Localized by key");
    }

    [Fact(DisplayName = "Localization falls back to ErrorCode when MessageKey is absent")]
    public async Task Localize_FallsBackTo_ErrorCode()
    {
        var localizer = new TestDictionaryLocalizer(new Dictionary<string, string>
        {
            ["by.code"] = "Localized by code"
        });

        var failure = new KyrolusValidationFailure("Name", "original message", ErrorCode: "by.code");
        var result = await RunSingleFailureAsync(failure, localizer);

        result.ErrorMessage.ShouldBe("Localized by code");
    }

    [Fact(DisplayName = "Localization falls back to the original ErrorMessage when no MessageKey, ErrorCode, or translation is found")]
    public async Task Localize_FallsBackTo_OriginalErrorMessage_WhenNoTranslationFound()
    {
        var localizer = new TestDictionaryLocalizer(new Dictionary<string, string>());

        var failure = new KyrolusValidationFailure("Name", "original message");
        var result = await RunSingleFailureAsync(failure, localizer);

        result.ErrorMessage.ShouldBe("original message");
    }

    [Fact(DisplayName = "Localization interpolates template placeholders from the failure itself")]
    public async Task Localize_Interpolates_FailureProperties()
    {
        var localizer = new TestDictionaryLocalizer(new Dictionary<string, string>
        {
            ["min.length"] = "Field {PropertyName} must be at least {AttemptedValue} chars"
        });

        var failure = new KyrolusValidationFailure("Username", "too short", MessageKey: "min.length", AttemptedValue: "abc");
        var result = await RunSingleFailureAsync(failure, localizer);

        result.ErrorMessage.ShouldBe("Field Username must be at least abc chars");
    }
}
