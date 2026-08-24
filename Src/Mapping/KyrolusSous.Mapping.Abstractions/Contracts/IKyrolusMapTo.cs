namespace KyrolusSous.Mapping.Abstractions.Contracts;

/// <summary>
/// Defines a contract for source types (Entities/Commands) that know their default destination representation <typeparamref name="TTarget"/>.
/// </summary>
/// <typeparam name="TTarget">The destination target type to map into.</typeparam>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// CQRS Command to Entity self-declaration:
/// <code>
/// public record CreateProductCommand(string Title, decimal Price) : IKyrolusMapTo&lt;Product&gt;;
/// </code>
/// </para>
/// </remarks>
public interface IKyrolusMapTo<TTarget>
{
}
