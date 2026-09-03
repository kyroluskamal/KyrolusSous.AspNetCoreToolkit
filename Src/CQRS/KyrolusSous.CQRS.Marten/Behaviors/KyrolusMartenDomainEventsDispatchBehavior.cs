using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Marten.Behaviors;

/// <summary>
/// Pipeline behavior collecting and dispatching domain events raised during Marten command handling.
/// </summary>
/// <remarks>
/// Collects <see cref="IDomainEventSource"/> from the request, the response, and - since
/// <c>AddRangeCommand</c>/<c>UpdateRangeCommand</c>/<c>BulkUpsertCommand</c> return
/// <c>IEnumerable&lt;TResponse&gt;</c> rather than a single entity - every item of a response that is
/// itself an entity collection. EF's equivalent behavior does not need this special case: it drains
/// <c>DbContext.ChangeTracker.Entries&lt;IDomainEventSource&gt;()</c>, which sees every entity touched
/// during the operation regardless of the command's declared response shape. Marten has no directly
/// analogous "every entity this unit of work touched" enumeration available to this package, so a
/// command whose response is neither the entity nor a collection of entities - <c>RemoveByIdCommand</c>,
/// <c>RemoveByEntityCommand</c>, <c>RemoveRangeCommand</c>, <c>SoftDeleteByIdCommand</c>,
/// <c>RestoreByIdCommand</c>, <c>BulkPatchCommand</c>, <c>ExecuteUpdateCommand</c>,
/// <c>ExecuteDeleteCommand</c> (all return a primitive or <see langword="void"/>, never the entity) -
/// still cannot have its domain events dispatched here. Closing that gap needs Marten session-level
/// introspection (e.g. <c>IDocumentSession.PendingChanges</c>) that this fix does not add.
/// </remarks>
[PipelineOrder(-650)]
public sealed class KyrolusMartenDomainEventsDispatchBehavior<TRequest, TResponse>(
    IKyrolusMediatorPublisher? publisher = null,
    ILogger<KyrolusMartenDomainEventsDispatchBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusMediatorPublisher? _publisher = publisher;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is not IKyrolusCommandBase || _publisher is null)
        {
            return response;
        }

        var sources = new List<IDomainEventSource>();
        CollectSource(request, sources);

        if (!ReferenceEquals(response, request))
        {
            if (!CollectSource(response, sources) && response is System.Collections.IEnumerable responseItems)
            {
                // Covers AddRangeCommand/UpdateRangeCommand/BulkUpsertCommand, whose response is
                // IEnumerable<TResponse> rather than a single entity - each item is checked
                // individually rather than the collection itself, which never implements
                // IDomainEventSource.
                foreach (var item in responseItems)
                {
                    CollectSource(item, sources);
                }
            }
        }

        foreach (var source in sources)
        {
            var events = source.DomainEvents.ToList();
            source.ClearDomainEvents();

            foreach (var domainEvent in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger?.LogDebug(
                    "[Kyrolus CQRS Marten] Dispatching domain event {EventType} from {SourceType}",
                    domainEvent.GetType().Name,
                    source.GetType().Name);

                await _publisher.PublishAsync((object)domainEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        return response;
    }

    private static bool CollectSource(object? candidate, List<IDomainEventSource> sources)
    {
        if (candidate is not IDomainEventSource source || source.DomainEvents.Count == 0) return false;
        sources.Add(source);
        return true;
    }
}
