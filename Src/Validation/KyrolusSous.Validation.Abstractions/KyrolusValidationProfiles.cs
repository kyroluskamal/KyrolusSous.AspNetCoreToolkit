namespace KyrolusSous.Validation.Abstractions;

public static class KyrolusValidationProfiles
{
    public static KyrolusValidationProfile Create { get; } = new(
        "Create",
        new KyrolusValidationContext(
            RuleSets: ["Create"],
            MinimumSeverity: KyrolusValidationSeverity.Error));

    public static KyrolusValidationProfile Update { get; } = new(
        "Update",
        new KyrolusValidationContext(
            RuleSets: ["Update"],
            MinimumSeverity: KyrolusValidationSeverity.Error));

    public static KyrolusValidationProfile UiHints { get; } = new(
        "UiHints",
        new KyrolusValidationContext(
            Groups: ["UiHints"],
            MinimumSeverity: KyrolusValidationSeverity.Info));

    public static KyrolusValidationProfile BackgroundJobs { get; } = new(
        "BackgroundJobs",
        new KyrolusValidationContext(
            RuleSets: ["BackgroundJobs"],
            MinimumSeverity: KyrolusValidationSeverity.Error));

    public static IReadOnlyList<KyrolusValidationProfile> All { get; } =
        [Create, Update, UiHints, BackgroundJobs];
}
