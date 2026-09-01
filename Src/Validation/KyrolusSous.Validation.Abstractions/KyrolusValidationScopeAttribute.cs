namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Tags a property with RuleSet and/or Group membership for DataAnnotations-based validators (both the
/// reflection-based validator and the source-generated one), mirroring the Fluent DSL's <c>RuleSet(...)</c>/
/// <c>Group(...)</c> scoping. Without this, a DataAnnotations-produced failure always carries the default
/// RuleSet/Group and silently disappears whenever a caller runs a scoped <see cref="KyrolusValidationContext"/>
/// (e.g. <c>RuleSets: ["Create"]</c>) that doesn't include the default scope.
/// </summary>
/// <example>
/// <code>
/// public class CreateUserRequest
/// {
///     [Required, MinLength(8)]
///     [KyrolusValidationScope(RuleSets = ["Create"])]
///     public string Password { get; set; } = string.Empty;
///
///     [Required]
///     [KyrolusValidationScope(Groups = ["Audit"])]
///     public int CreatedBy { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusValidationScopeAttribute : Attribute
{
    /// <summary>The RuleSet scenario names (e.g. "Create", "Update") this property's rules belong to.</summary>
    public string[] RuleSets { get; set; } = [];

    /// <summary>The logical Group tags (e.g. "UiHints", "Audit") this property's rules belong to.</summary>
    public string[] Groups { get; set; } = [];
}
