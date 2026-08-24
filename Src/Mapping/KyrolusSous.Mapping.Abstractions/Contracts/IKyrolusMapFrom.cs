namespace KyrolusSous.Mapping.Abstractions.Contracts;

/// <summary>
/// Defines a contract for destination types (DTOs/ViewModels) that know how to construct themselves from a source entity <typeparamref name="TSource"/>.
/// </summary>
/// <typeparam name="TSource">The origin source type to map from.</typeparam>
/// <remarks>
/// <para>
/// <b>Real-World Use Case:</b>
/// Clean Architecture self-registering DTOs:
/// <code>
/// public record UserSummaryDto(Guid Id, string Name) : IKyrolusMapFrom&lt;User&gt;;
/// </code>
/// </para>
/// </remarks>
public interface IKyrolusMapFrom<TSource>
{
}
