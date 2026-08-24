namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Instructs the compile-time Roslyn Source Generator and runtime mapping engine to generate mapping logic from the decorated type to <see cref="TargetType"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is Bidirectional Mapping (<see cref="IsBidirectional"/>)?</b>
/// By default (<c>IsBidirectional = false</c>), mapping code is only generated in one direction: from the decorated source type to <see cref="TargetType"/>.
/// When setting <see cref="IsBidirectional"/> to <c>true</c>, the mapping engine and source generator automatically generate mapping logic in <b>both directions</b>:
/// <list type="bullet">
///   <item><description>Forward mapping: <c>Source -> Target</c> (e.g. <c>customer.ToCustomerDto()</c>)</description></item>
///   <item><description>Reverse mapping: <c>Target -> Source</c> (e.g. <c>customerDto.ToCustomer()</c>)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Real-World Use Cases &amp; Syntax Examples:</b>
/// <list type="number">
///   <item>
///     <description>
///       <b>Standard One-Way Mapping:</b>
///       <code>
///       [KyrolusMapTo(typeof(CustomerDto))]
///       public class Customer
///       {
///           public Guid Id { get; set; }
///           public string Name { get; set; } = string.Empty;
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Bidirectional Mapping via Named Property Argument:</b>
///       <code>
///       [KyrolusMapTo(typeof(CustomerDto), IsBidirectional = true)]
///       public class Customer
///       {
///           public Guid Id { get; set; }
///           public string Name { get; set; } = string.Empty;
///       }
///       </code>
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Bidirectional Mapping via Constructor Parameter:</b>
///       <code>
///       [KyrolusMapTo(typeof(CustomerDto), true)]
///       public class Customer
///       {
///           public Guid Id { get; set; }
///           public string Name { get; set; } = string.Empty;
///       }
///       </code>
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class KyrolusMapToAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusMapToAttribute"/> class targeting the specified destination type.
    /// </summary>
    /// <param name="targetType">The destination type to map into.</param>
    /// <example>
    /// <code>
    /// [KyrolusMapTo(typeof(UserResponseDto))]
    /// public class UserEntity { ... }
    /// </code>
    /// </example>
    public KyrolusMapToAttribute(Type targetType)
    {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusMapToAttribute"/> class with an explicit bidirectional mapping option.
    /// </summary>
    /// <param name="targetType">The destination type to map into.</param>
    /// <param name="isBidirectional">
    /// If <c>true</c>, generates mapping operations in both directions (Source to Target and Target to Source).
    /// </param>
    /// <example>
    /// <code>
    /// [KyrolusMapTo(typeof(ProductDto), isBidirectional: true)]
    /// public class Product { ... }
    /// </code>
    /// </example>
    public KyrolusMapToAttribute(Type targetType, bool isBidirectional)
    {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        IsBidirectional = isBidirectional;
    }

    /// <summary>
    /// Gets the destination target type.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Gets or sets whether mapping should be performed bidirectionally (generating both Source-to-Target and Target-to-Source operations).
    /// </summary>
    /// <remarks>
    /// Can be set via named attribute property syntax: <c>[KyrolusMapTo(typeof(Dto), IsBidirectional = true)]</c>.
    /// </remarks>
    public bool IsBidirectional { get; set; }
}
