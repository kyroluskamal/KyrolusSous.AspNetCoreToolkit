namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// Specifies the rule execution cascade mode across multiple rules in a validator.
/// </summary>
public enum KyrolusCascadeMode
{
    /// <summary>Continue executing all rules even if previous rules have failed.</summary>
    Continue,

    /// <summary>Stop rule execution immediately upon encountering the first failure in the validator.</summary>
    Stop
}
