namespace KyrolusSous.Repositories.EF.Abstractions.Events;

/// <summary>
/// Defines an entity that records domain events to be published during Unit of Work commit.
/// </summary>
public interface IKyrolusHasDomainEvents
{
    /// <summary>
    /// Gets the read-only collection of queued domain events.
    /// </summary>
    IReadOnlyCollection<object> DomainEvents { get; }

    /// <summary>
    /// Adds a domain event to the entity's event queue.
    /// </summary>
    void AddDomainEvent(object domainEvent);

    /// <summary>
    /// Clears all queued domain events after they have been published.
    /// </summary>
    void ClearDomainEvents();
}

/// <summary>
/// Dispatches domain events collected from tracked entities.
/// </summary>
public interface IKyrolusDomainEventDispatcher
{
    /// <summary>
    /// Dispatches the collection of domain events asynchronously.
    /// </summary>
    Task DispatchEventsAsync(IEnumerable<object> domainEvents, CancellationToken cancellationToken = default);
}
