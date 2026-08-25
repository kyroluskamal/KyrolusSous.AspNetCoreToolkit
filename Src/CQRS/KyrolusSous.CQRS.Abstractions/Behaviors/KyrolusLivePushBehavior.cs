using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.LivePush;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Pipeline behavior broadcasting real-time notifications upon successful execution of <see cref="ILivePushCommand"/>.
/// </summary>
[PipelineOrder(-550)]
public sealed class KyrolusLivePushBehavior<TRequest, TResponse>(
    ILivePushPublisher? publisher = null,
    ILogger<KyrolusLivePushBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly ILivePushPublisher? _publisher = publisher;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is ILivePushCommand liveCommand && _publisher is not null)
        {
            try
            {
                var payload = liveCommand.PushData ?? response ?? (object)request!;
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
