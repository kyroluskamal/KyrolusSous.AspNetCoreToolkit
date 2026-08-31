namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Contains default constant string and fallback values used throughout the validation system.
/// </summary>
public static class KyrolusValidationDefaults
{
    /// <summary>The default RuleSet name ("default") used when no RuleSet is specified.</summary>
    public const string DefaultRuleSet = "default";

    /// <summary>The default Group name ("default") assigned when no Group tag is specified.</summary>
    public const string DefaultGroup = "default";

    /// <summary>The standard fallback error message ("Validation failed.") when no custom message is provided.</summary>
    public const string DefaultErrorMessage = "Validation failed.";
}
