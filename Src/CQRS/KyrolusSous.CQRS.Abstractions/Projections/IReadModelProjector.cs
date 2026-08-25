namespace KyrolusSous.CQRS.Abstractions.Projections;

/// <summary>
/// Handles synchronization and projection updates for a given read-model type.
/// </summary>
/// <typeparam name="TReadModel">The read model type being projected.</typeparam>
public interface IReadModelProjector<in TReadModel>
{
    /// <summary>
    /// Projects or updates the read model in its secondary store (e.g. Elasticsearch, Redis, or read database).
    /// </summary>
    Task ProjectAsync(TReadModel model, CancellationToken cancellationToken = default);
}
