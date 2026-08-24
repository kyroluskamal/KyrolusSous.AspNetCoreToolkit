namespace KyrolusSous.Repositories.Marten.Runtime.EventStore;

/// <summary>
/// Default Marten-backed implementation of <see cref="IKyrolusMartenEventStore"/>.
/// </summary>
public class KyrolusMartenEventStore(IDocumentSession session) : IKyrolusMartenEventStore
{
    private readonly IDocumentSession session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task AppendEventsAsync<TId>(TId streamId, IEnumerable<object> events, long? expectedVersion = null, CancellationToken cancellationToken = default) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(events);
        var eventStore = session.Events;
        var key = streamId.ToString() ?? throw new ArgumentNullException(nameof(streamId));
        if (expectedVersion.HasValue)
            eventStore.Append(key, expectedVersion.Value, [.. events]);
        else
            eventStore.Append(key, [.. events]);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<object>> LoadStreamAsync<TId>(TId streamId, long fromVersion = 0, CancellationToken cancellationToken = default) where TId : notnull
    {
        var key = streamId.ToString() ?? throw new ArgumentNullException(nameof(streamId));
        var stream = await session.Events.FetchStreamAsync(key, fromVersion, token: cancellationToken).ConfigureAwait(false);
        return [.. stream.Select(e => e.Data)];
    }

    public Task<TAggregate?> AggregateStreamAsync<TAggregate, TId>(TId streamId, long version = 0, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default)
        where TAggregate : class
        where TId : notnull
    {
        var key = streamId.ToString() ?? throw new ArgumentNullException(nameof(streamId));
        return session.Events.AggregateStreamAsync<TAggregate>(key, version, timestamp, token: cancellationToken);
    }

    public async Task<bool> StreamExistsAsync<TId>(TId streamId, CancellationToken cancellationToken = default) where TId : notnull
    {
        var key = streamId.ToString() ?? throw new ArgumentNullException(nameof(streamId));
        var state = await session.Events.FetchStreamStateAsync(key, token: cancellationToken).ConfigureAwait(false);
        return state is not null;
    }

    public async Task ArchiveStreamAsync<TId>(TId streamId, CancellationToken cancellationToken = default) where TId : notnull
    {
        var key = streamId.ToString() ?? throw new ArgumentNullException(nameof(streamId));
        session.Events.ArchiveStream(key);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
