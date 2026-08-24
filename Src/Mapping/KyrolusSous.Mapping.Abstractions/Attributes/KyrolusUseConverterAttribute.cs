namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Specifies a custom <see cref="IKyrolusTypeConverter{TSource, TTarget}"/> or <see cref="IKyrolusValueResolver{TSource, TTarget, TMember}"/>
/// to be used for converting the decorated type or member.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Applying custom parsing rules (e.g. converting a comma-separated string to an array or parsing special ISO timestamps):
/// <code>
/// public class ProductDto
/// {
///     [KyrolusUseConverter(typeof(CommaSeparatedTagConverter))]
///     public string[] Tags { get; set; } = [];
/// }
/// </code>
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="KyrolusUseConverterAttribute"/> class.
/// </remarks>
/// <param name="converterType">The type implementing <see cref="IKyrolusTypeConverter{TSource, TTarget}"/> or <see cref="IKyrolusValueResolver{TSource, TTarget, TMember}"/>.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusUseConverterAttribute(Type converterType) : Attribute
{


    /// <summary>
    /// Gets the custom converter type.
    /// </summary>
    public Type ConverterType { get; } = converterType ?? throw new ArgumentNullException(nameof(converterType));
}
