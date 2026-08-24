namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Instructs the mapping engine and source generator to skip copying properties when the source value is <c>null</c> (HTTP PATCH semantics).
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Applying partial updates (PATCH requests) to database entities without wiping out existing non-null database fields:
/// <code>
/// [KyrolusIgnoreNull]
/// public class UpdateCustomerRequest
/// {
///     public string? Name { get; set; } // If null, existing Customer.Name is NOT overwritten
///     public string? Phone { get; set; }
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class KyrolusIgnoreNullAttribute : Attribute
{
}
