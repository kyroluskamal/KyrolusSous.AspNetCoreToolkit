namespace KyrolusSous.Validation.Abstractions;

public sealed record KyrolusValidationContext(
    IReadOnlyCollection<string>? RuleSets = null,
    IReadOnlyCollection<string>? Groups = null,
    KyrolusValidationSeverity? MinimumSeverity = null,
    IReadOnlyCollection<string>? Profiles = null)
{
    public static KyrolusValidationContext Default { get; } = new();
}
