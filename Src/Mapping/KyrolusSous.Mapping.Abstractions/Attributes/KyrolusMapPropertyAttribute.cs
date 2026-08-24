namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Explicitly maps a member or parameter to a differently-named source or target property.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Mapping database column properties to differently named API DTO fields:
/// <code>
/// public class CustomerDto
/// {
///     [KyrolusMapProperty("CustomerFullName")]
///     public string Name { get; set; } = string.Empty;
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusMapPropertyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusMapPropertyAttribute"/> class.
    /// </summary>
    /// <param name="sourceName">The name of the source property to map from.</param>
    public KyrolusMapPropertyAttribute(string sourceName)
    {
        SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
    }

    /// <summary>
    /// Gets the name of the source property to map from.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// Gets or sets an alternate target property name when mapping in the reverse direction.
    /// </summary>
    public string? TargetName { get; set; }
}
