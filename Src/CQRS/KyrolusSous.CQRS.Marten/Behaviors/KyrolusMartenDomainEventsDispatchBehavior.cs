using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Marten.Behaviors;

/// <summary>
/// Pipeline behavior collecting and dispatching domain events raised during Marten command handling.
/// </summary>
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
        if (request is IDomainEventSource requestSource && requestSource.DomainEvents.Count > 0)
        {
            sources.Add(requestSource);
        }
        if (response is IDomainEventSource responseSource && responseSource.DomainEvents.Count > 0 && !ReferenceEquals(responseSource, request))
        {
            sources.Add(responseSource);
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
}
