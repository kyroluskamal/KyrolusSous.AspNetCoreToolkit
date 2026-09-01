namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Shared RuleSet/Group scope-matching logic used by every validator kind (Fluent, the reflection-based
/// DataAnnotations validator, and the source-generated DataAnnotations validators) so a rule or property tagged
/// with RuleSets/Groups behaves identically under <see cref="KyrolusValidationContext"/> filtering no matter which
/// validator produced the failure.
/// </summary>
public static class KyrolusValidationScopeResolver
{
    /// <summary>
    /// Determines whether a rule/property with the given static RuleSet/Group tags should run for the given
    /// context. An untagged (empty) scope only runs when the context selects the matching default scope, "*", or
    /// nothing at all.
    /// </summary>
    public static bool ShouldExecute(
        IEnumerable<string>? contextRuleSets,
        IReadOnlyCollection<string> ruleRuleSets,
        IEnumerable<string>? contextGroups,
        IReadOnlyCollection<string> ruleGroups)
    {
        return ShouldExecuteScope(contextRuleSets, ruleRuleSets, KyrolusValidationDefaults.DefaultRuleSet)
            && ShouldExecuteScope(contextGroups, ruleGroups, KyrolusValidationDefaults.DefaultGroup);
    }

    /// <summary>
    /// Determines whether a single scope dimension (RuleSets or Groups) should execute: true when the context
    /// doesn't filter on this dimension at all, requests "*", the rule declares no scope but the context selects
    /// the default one, or the rule and context scopes overlap.
    /// </summary>
    public static bool ShouldExecuteScope(
        IEnumerable<string>? selectedScopes,
        IEnumerable<string> ruleScopes,
        string defaultScope)
    {
        if (selectedScopes is null || !selectedScopes.Any() || selectedScopes.Contains("*", StringComparer.OrdinalIgnoreCase))
            return true;
        if (!ruleScopes.Any())
            return selectedScopes.Contains(defaultScope, StringComparer.OrdinalIgnoreCase);

        return ruleScopes.Any(ruleScope => selectedScopes.Contains(ruleScope, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Picks the single RuleSet value to stamp on a produced failure: the rule's tag that matches a
    /// context-requested RuleSet, the rule's first tag when the context requests "*", or null when the context
    /// requests no specific RuleSets at all (leaving the failure's RuleSet unchanged).
    /// </summary>
    public static string? ResolveActiveRuleSet(
        IReadOnlyCollection<string> ruleRuleSets,
        IReadOnlyCollection<string>? contextRuleSets)
    {
        if (contextRuleSets is not { Count: > 0 })
            return null;

        return ruleRuleSets.FirstOrDefault(r => contextRuleSets.Contains(r, StringComparer.OrdinalIgnoreCase))
            ?? (contextRuleSets.Contains("*") ? ruleRuleSets.FirstOrDefault() : contextRuleSets.First());
    }
}
