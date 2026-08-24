namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

/// <summary>
/// Event store contract for Marten-based event sourcing systems.
/// </summary>
public interface IKyrolusMartenEventStore
{
    /// <summary>
    /// Appends events to a stream with optional optimistic concurrency expected version check.
    /// </summary>
    Task AppendEventsAsync<TId>(TId streamId, IEnumerable<object> events, long? expectedVersion = null, CancellationToken cancellationToken = default)
        where TId : notnull;

    /// <summary>
    /// Loads all raw events for a stream starting from a specified version.
    /// </summary>
    Task<IReadOnlyList<object>> LoadStreamAsync<TId>(TId streamId, long fromVersion = 0, CancellationToken cancellationToken = default)
        where TId : notnull;

    /// <summary>
    /// Hydrates and computes the current aggregate state by applying stream events.
    /// </summary>
    Task<TAggregate?> AggregateStreamAsync<TAggregate, TId>(TId streamId, long version = 0, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default)
        where TAggregate : class
        where TId : notnull;

    /// <summary>
    /// Checks whether an event stream exists.
    /// </summary>
    Task<bool> StreamExistsAsync<TId>(TId streamId, CancellationToken cancellationToken = default)
        where TId : notnull;

    /// <summary>
    /// Archives or tombstones an event stream (GDPR / lifecycle compliance).
    /// </summary>
    Task ArchiveStreamAsync<TId>(TId streamId, CancellationToken cancellationToken = default)
        where TId : notnull;
}
