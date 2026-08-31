namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Encapsulates execution parameters that control which rule sets execute, which group tags are filtered,
/// and the minimum severity threshold for collected failures.
/// </summary>
/// <param name="RuleSets">Collection of active RuleSet names to execute (e.g. "Create", "Update", "*").</param>
/// <param name="Groups">Collection of target Group tags to filter failures by (e.g. "UiHints", "Security").</param>
/// <param name="MinimumSeverity">Minimum severity required for failures to be reported.</param>
/// <param name="Profiles">Names of pre-registered validation profiles to apply.</param>
/// <example>
/// <code>
/// // Execute only "Create" scenario rules and filter for "UiHints" tags
/// var context = new KyrolusValidationContext(
///     RuleSets: ["Create"],
///     Groups: ["UiHints"],
///     MinimumSeverity: KyrolusValidationSeverity.Warning);
/// 
/// var failures = await engine.ValidateAsync(model, context);
/// </code>
/// </example>
public sealed record KyrolusValidationContext(
    IReadOnlyCollection<string>? RuleSets = null,
    IReadOnlyCollection<string>? Groups = null,
    KyrolusValidationSeverity? MinimumSeverity = null,
    IReadOnlyCollection<string>? Profiles = null)
{
    /// <summary>Gets a singleton instance of the default validation context (no filters).</summary>
    public static KyrolusValidationContext Default { get; } = new();
}
