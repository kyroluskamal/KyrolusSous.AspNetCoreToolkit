namespace KyrolusSous.Validation.Fluent;

/// <summary>
/// Defines an internal executable validation rule unit with associated RuleSets and Group tags.
/// </summary>
/// <typeparam name="T">The type of the model being validated.</typeparam>
public interface IKyrolusValidationRule<in T>
{
    /// <summary>Gets the RuleSets this rule belongs to.</summary>
    IReadOnlyCollection<string> RuleSets { get; }

    /// <summary>Gets the Group tags assigned to this rule.</summary>
    IReadOnlyCollection<string> Groups { get; }

    /// <summary>Executes the validation rule against the target instance.</summary>
    ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(T request, CancellationToken cancellationToken = default);
}
