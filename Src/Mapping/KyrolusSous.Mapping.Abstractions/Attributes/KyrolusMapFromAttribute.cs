namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Instructs the compile-time Roslyn Source Generator and runtime mapping engine to generate mapping logic from <see cref="SourceType"/> to the decorated destination type.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is Bidirectional Mapping (<see cref="IsBidirectional"/>)?</b>
/// By default (<c>IsBidirectional = false</c>), mapping code is only generated from <see cref="SourceType"/> into the decorated type.
/// When setting <see cref="IsBidirectional"/> to <c>true</c>, the mapping engine and source generator automatically generate mapping logic in <b>both directions</b>:
/// <list type="bullet">
///   <item><description>Forward mapping: <c>Source -> Target</c> (e.g. <c>productEntity.ToProductResponseDto()</c>)</description></item>
///   <item><description>Reverse mapping: <c>Target -> Source</c> (e.g. <c>productResponseDto.ToProductEntity()</c>)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Real-World Use Cases &amp; Syntax Examples:</b>
/// <list type="number">
///   <item>
///     <description>
///       <b>Standard One-Way Mapping:</b>
///       <code>
///       [KyrolusMapFrom(typeof(ProductEntity))]
///       public record ProductResponseDto(int Id, string Name, decimal Price);
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Bidirectional Mapping via Named Property Argument:</b>
///       <code>
///       [KyrolusMapFrom(typeof(ProductEntity), IsBidirectional = true)]
///       public record ProductResponseDto(int Id, string Name, decimal Price);
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Bidirectional Mapping via Constructor Parameter:</b>
///       <code>
///       [KyrolusMapFrom(typeof(ProductEntity), true)]
///       public record ProductResponseDto(int Id, string Name, decimal Price);
///       </code>
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class KyrolusMapFromAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusMapFromAttribute"/> class with the specified source origin type.
    /// </summary>
    /// <param name="sourceType">The source origin type to map from.</param>
    /// <example>
    /// <code>
    /// [KyrolusMapFrom(typeof(OrderEntity))]
    /// public class OrderDto { ... }
    /// </code>
    /// </example>
    public KyrolusMapFromAttribute(Type sourceType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusMapFromAttribute"/> class with an explicit bidirectional mapping option.
    /// </summary>
    /// <param name="sourceType">The source origin type to map from.</param>
    /// <param name="isBidirectional">
    /// If <c>true</c>, generates mapping operations in both directions (Source to Target and Target to Source).
    /// </param>
    /// <example>
    /// <code>
    /// [KyrolusMapFrom(typeof(OrderEntity), isBidirectional: true)]
    /// public class OrderDto { ... }
    /// </code>
    /// </example>
    public KyrolusMapFromAttribute(Type sourceType, bool isBidirectional)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        IsBidirectional = isBidirectional;
    }

    /// <summary>
    /// Gets the source origin type.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets or sets whether mapping should be performed bidirectionally (generating both Source-to-Target and Target-to-Source operations).
    /// </summary>
    /// <remarks>
    /// Can be set via named attribute property syntax: <c>[KyrolusMapFrom(typeof(Entity), IsBidirectional = true)]</c>.
    /// </remarks>
    public bool IsBidirectional { get; set; }
}
