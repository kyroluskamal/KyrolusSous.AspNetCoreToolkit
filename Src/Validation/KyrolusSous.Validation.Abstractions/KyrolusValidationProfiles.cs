namespace KyrolusSous.Validation.Abstractions;

public static class KyrolusValidationProfiles
{
    public static KyrolusValidationProfile Create { get; } = new(
        "Create",
        new KyrolusValidationContext(
            RuleSets: new[] { "Create" },
            MinimumSeverity: KyrolusValidationSeverity.Error));

    public static KyrolusValidationProfile Update { get; } = new(
        "Update",
        new KyrolusValidationContext(
            RuleSets: new[] { "Update" },
            MinimumSeverity: KyrolusValidationSeverity.Error));

    public static KyrolusValidationProfile UiHints { get; } = new(
        "UiHints",
        new KyrolusValidationContext(
            Groups: new[] { "UiHints" },
            MinimumSeverity: KyrolusValidationSeverity.Info));

    public static KyrolusValidationProfile BackgroundJobs { get; } = new(
        "BackgroundJobs",
        new KyrolusValidationContext(
            RuleSets: new[] { "BackgroundJobs" },
            MinimumSeverity: KyrolusValidationSeverity.Error));

    public static IReadOnlyList<KyrolusValidationProfile> All { get; } =
        new[] { Create, Update, UiHints, BackgroundJobs };
}
