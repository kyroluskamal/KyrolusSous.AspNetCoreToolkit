namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Provides built-in, pre-configured validation profiles for common application lifecycle scenarios.
/// </summary>
/// <example>
/// <code>
/// // Register all built-in profiles into DI
/// services.AddKyrolusValidationProfiles(KyrolusValidationProfiles.All);
/// 
/// // Use profile by name in context
/// var context = new KyrolusValidationContext(Profiles: [KyrolusValidationProfiles.Create.Name]);
/// var failures = await engine.ValidateAsync(command, context);
/// </code>
/// </example>
public static class KyrolusValidationProfiles
{
    /// <summary>Profile for entity creation workflows (RuleSets: ["Create"], Severity: Error).</summary>
    public static KyrolusValidationProfile Create { get; } = new(
        "Create",
        new KyrolusValidationContext(
            RuleSets: ["Create"],
            MinimumSeverity: KyrolusValidationSeverity.Error));

    /// <summary>Profile for entity update workflows (RuleSets: ["Update"], Severity: Error).</summary>
    public static KyrolusValidationProfile Update { get; } = new(
        "Update",
        new KyrolusValidationContext(
            RuleSets: ["Update"],
            MinimumSeverity: KyrolusValidationSeverity.Error));

    /// <summary>Profile for UI hint queries (Groups: ["UiHints"], Severity: Info).</summary>
    public static KyrolusValidationProfile UiHints { get; } = new(
        "UiHints",
        new KyrolusValidationContext(
            Groups: ["UiHints"],
            MinimumSeverity: KyrolusValidationSeverity.Info));

    /// <summary>Profile for background job processing (RuleSets: ["BackgroundJobs"], Severity: Error).</summary>
    public static KyrolusValidationProfile BackgroundJobs { get; } = new(
        "BackgroundJobs",
        new KyrolusValidationContext(
            RuleSets: ["BackgroundJobs"],
            MinimumSeverity: KyrolusValidationSeverity.Error));

    /// <summary>Gets a list containing all default pre-configured validation profiles.</summary>
    public static IReadOnlyList<KyrolusValidationProfile> All { get; } =
        [Create, Update, UiHints, BackgroundJobs];
}
