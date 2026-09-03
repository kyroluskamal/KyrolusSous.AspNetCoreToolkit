namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Contract for aggregates or entities that raise domain events during business command handling.
/// </summary>
public interface IKyrolusDomainEventSource
{
    /// <summary>
    /// Collection of raised domain events pending dispatch.
    /// </summary>
    IReadOnlyCollection<object> DomainEvents { get; }

    /// <summary>
    /// Clears all raised domain events after successful dispatch.
    /// </summary>
    void ClearDomainEvents();
}
