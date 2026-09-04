using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.LivePush;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior broadcasting real-time notifications upon successful execution of <see cref="IKyrolusLivePushCommand"/>.
/// </summary>
/// <remarks>
/// Ordered outer (more negative) than <c>KyrolusReadModelProjectionBehavior</c> (-600),
/// <c>KyrolusCommandCacheInvalidationBehavior</c> (-560, in <c>KyrolusSous.CQRS.Caching</c>) and the
/// EF/Marten <c>DomainEventsDispatchBehavior</c> (-650), so the broadcast is the LAST post-write side
/// effect to run, after all of those have already updated their own state. Broadcasting first (its
/// previous position, -550, inside all three) meant a subscriber that reacts to the push by
/// immediately re-querying could still see a stale cached response or a stale read model - the
/// notification arrived before the things it was announcing were actually done.
/// </remarks>
[PipelineOrder(-660)]
public sealed class KyrolusLivePushBehavior<TRequest, TResponse>(
    IKyrolusLivePushPublisher? publisher = null,
    ILogger<KyrolusLivePushBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusLivePushPublisher? _publisher = publisher;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is IKyrolusLivePushCommand liveCommand && _publisher is not null)
        {
            try
            {
                var payload = liveCommand.PushData ?? response ?? (object)request;
                await _publisher.PublishLiveAsync(liveCommand.Channel, payload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[Kyrolus CQRS LivePush] Failed to broadcast live notification on channel '{Channel}'", liveCommand.Channel);
            }
        }

        return response;
    }
}
