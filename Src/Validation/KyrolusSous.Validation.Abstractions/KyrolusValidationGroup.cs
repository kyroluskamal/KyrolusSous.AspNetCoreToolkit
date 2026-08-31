namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Container holding a collection of logical group names / tags for categorization of validation rules.
/// </summary>
/// <example>
/// <code>
/// // Single group
/// var group1 = new KyrolusValidationGroup("UiHints");
/// 
/// // Multiple groups via params
/// var group2 = new KyrolusValidationGroup("Account", "Security", "Audit");
/// 
/// // Using with FluentValidation state
/// RuleFor(x => x.Email).NotEmpty().WithState(_ => new KyrolusValidationGroup("UiHints"));
/// </code>
/// </example>
public sealed record KyrolusValidationGroup
{
    /// <summary>
    /// Gets the collection of group names associated with this group container.
    /// </summary>
    public IReadOnlyList<string> Names { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusValidationGroup"/> with one or more group names.
    /// </summary>
    /// <param name="names">The array of group names.</param>
    public KyrolusValidationGroup(params string[] names)
        : this((IEnumerable<string>)names)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusValidationGroup"/> with a collection of group names.
    /// </summary>
    /// <param name="names">The collection of group names.</param>
    public KyrolusValidationGroup(IEnumerable<string> names)
    {
        Names = names?
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }
}
