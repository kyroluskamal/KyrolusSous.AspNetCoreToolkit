using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.EF.Behaviors;

/// <summary>
/// Pipeline behavior automatically collecting and dispatching domain events raised by modified entities during command execution.
/// </summary>
[PipelineOrder(-650)]
public sealed class KyrolusDomainEventsDispatchBehavior<TRequest, TResponse, TDbContext>(
    IKyrolusMediatorPublisher? publisher = null,
    TDbContext? dbContext = null,
    ILogger<KyrolusDomainEventsDispatchBehavior<TRequest, TResponse, TDbContext>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
    where TDbContext : DbContext
{
    private readonly IKyrolusMediatorPublisher? _publisher = publisher;
    private readonly TDbContext? _dbContext = dbContext;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is not IKyrolusCommandBase || _publisher is null || _dbContext is null)
        {
            return response;
        }

        while (true)
        {
            var domainEntities = _dbContext.ChangeTracker
                .Entries<IDomainEventSource>()
                .Where(x => x.Entity.DomainEvents.Count > 0)
                .Select(x => x.Entity)
                .ToList();

            if (domainEntities.Count == 0)
            {
                break;
            }

            foreach (var entity in domainEntities)
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();

                foreach (var domainEvent in events)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger?.LogDebug(
                        "[Kyrolus CQRS EF] Dispatching domain event {EventType} from entity {EntityType}",
                        domainEvent.GetType().Name,
                        entity.GetType().Name);

                    await _publisher.PublishAsync((object)domainEvent, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return response;
    }
}
