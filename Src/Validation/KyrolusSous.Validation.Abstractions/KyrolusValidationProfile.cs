namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Represents a named validation profile bundling preset <see cref="KyrolusValidationContext"/> settings.
/// </summary>
/// <param name="Name">The unique profile identifier (e.g. "Create", "Update", "UiHints").</param>
/// <param name="Context">The context settings bundled with this profile.</param>
/// <example>
/// <code>
/// var adminProfile = new KyrolusValidationProfile(
///     "Admin",
///     new KyrolusValidationContext(
///         RuleSets: ["Admin", "Audit"],
///         MinimumSeverity: KyrolusValidationSeverity.Warning));
/// </code>
/// </example>
public sealed record KyrolusValidationProfile(
    string Name,
    KyrolusValidationContext Context);
