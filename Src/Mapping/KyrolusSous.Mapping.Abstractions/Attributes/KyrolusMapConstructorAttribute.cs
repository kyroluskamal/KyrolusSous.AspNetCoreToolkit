namespace KyrolusSous.Mapping.Abstractions.Attributes;

/// <summary>
/// Explicitly marks the constructor to be used by the mapping engine or source generator when instantiating target objects with multiple constructors.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// In domain entities with rich constructors (e.g. DDD entities with validation rules) where the mapper should use a specific factory constructor rather than default:
/// <code>
/// public class Order
/// {
///     public Order() { }
///     
///     [KyrolusMapConstructor]
///     public Order(Guid id, string customerCode, decimal total)
///     {
///         Id = id;
///         CustomerCode = customerCode;
///         Total = total;
///     }
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class KyrolusMapConstructorAttribute : Attribute
{
}
