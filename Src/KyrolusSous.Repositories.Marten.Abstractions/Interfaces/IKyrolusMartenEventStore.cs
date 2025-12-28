using Marten.Events;

namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

/// <summary>
/// Event store contract for Marten-based systems.
/// </summary>
public interface IKyrolusMartenEventStore
{
    Task AppendEventsAsync<TId>(TId streamId, IEnumerable<object> events, long? expectedVersion = null, CancellationToken cancellationToken = default)
        where TId : notnull;

    Task<IReadOnlyList<object>> LoadStreamAsync<TId>(TId streamId, long fromVersion = 0, CancellationToken cancellationToken = default)
        where TId : notnull;

    Task<bool> StreamExistsAsync<TId>(TId streamId, CancellationToken cancellationToken = default)
        where TId : notnull;
}
