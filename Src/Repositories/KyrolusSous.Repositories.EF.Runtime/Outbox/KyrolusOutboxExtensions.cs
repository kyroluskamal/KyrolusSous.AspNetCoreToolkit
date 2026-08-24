using System.Text.Json;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Outbox;

namespace KyrolusSous.Repositories.EF.Runtime.Outbox;

/// <summary>
/// Provides transactional outbox extensions for <see cref="IKyrolusUnitOfWork"/>.
/// </summary>
public static class KyrolusOutboxExtensions
{
    /// <summary>
    /// Enqueues a domain integration event into the outbox within the current unit of work transaction.
    /// </summary>
    public static async Task AddOutboxMessageAsync<TEvent>(
        this IKyrolusUnitOfWork unitOfWork,
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(domainEvent);

        var message = new KyrolusOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = typeof(TEvent).AssemblyQualifiedName ?? typeof(TEvent).FullName ?? typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(domainEvent),
            OccurredOnUtc = DateTime.UtcNow
        };

        if (unitOfWork is IKyrolusOutboxStore store)
        {
            await store.EnqueueAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
