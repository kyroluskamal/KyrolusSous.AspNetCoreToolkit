using System.Text.Json;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Outbox;

namespace KyrolusSous.Repositories.Marten.Runtime.Outbox;

/// <summary>
/// Transactional outbox extensions for Marten document store and unit of work.
/// </summary>
public static class KyrolusMartenOutboxExtensions
{
    /// <summary>
    /// Enqueues an integration domain event as an outbox message within the active <see cref="IDocumentSession"/>.
    /// </summary>
    public static void AddOutboxMessage<TEvent>(
        this IDocumentSession session,
        TEvent domainEvent)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(domainEvent);

        var message = new KyrolusMartenOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = typeof(TEvent).AssemblyQualifiedName ?? typeof(TEvent).FullName ?? typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(domainEvent),
            OccurredOnUtc = DateTime.UtcNow,
            Processed = false
        };

        session.Store(message);
    }

    /// <summary>
    /// Enqueues an outbox message within the current unit of work transaction.
    /// </summary>
    public static Task AddOutboxMessageAsync<TSession, TEvent>(
        this IKyrolusMartenUnitOfWork<TSession> unitOfWork,
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TSession : class
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (unitOfWork is IKyrolusMartenOutboxStore store)
        {
            var message = new KyrolusMartenOutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = typeof(TEvent).AssemblyQualifiedName ?? typeof(TEvent).FullName ?? typeof(TEvent).Name,
                Payload = JsonSerializer.Serialize(domainEvent),
                OccurredOnUtc = DateTime.UtcNow,
                Processed = false
            };

            return store.EnqueueAsync(message, cancellationToken);
        }

        throw new InvalidOperationException($"Unit of work '{unitOfWork.GetType().Name}' does not implement '{nameof(IKyrolusMartenOutboxStore)}'.");
    }
}
