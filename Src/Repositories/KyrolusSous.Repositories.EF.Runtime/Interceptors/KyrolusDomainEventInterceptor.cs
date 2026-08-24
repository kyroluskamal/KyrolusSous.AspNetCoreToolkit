using KyrolusSous.Repositories.EF.Abstractions.Events;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KyrolusSous.Repositories.EF.Runtime.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that collects domain events from tracked <see cref="IKyrolusHasDomainEvents"/> entities and dispatches them asynchronously.
/// </summary>
public sealed class KyrolusDomainEventInterceptor(IKyrolusDomainEventDispatcher? dispatcher = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null && dispatcher is not null)
        {
            DispatchDomainEventsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && dispatcher is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var domainEntities = context.ChangeTracker
            .Entries<IKyrolusHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        foreach (var entityEntry in domainEntities)
        {
            entityEntry.Entity.ClearDomainEvents();
        }

        if (domainEvents.Count > 0 && dispatcher is not null)
        {
            await dispatcher.DispatchEventsAsync(domainEvents, cancellationToken).ConfigureAwait(false);
        }
    }
}
