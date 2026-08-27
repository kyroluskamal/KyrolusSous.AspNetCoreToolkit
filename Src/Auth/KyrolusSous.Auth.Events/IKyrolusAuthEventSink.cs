using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Auth.Events;

/// <summary>
/// Handler contract for reacting to a specific authentication or security event.
/// </summary>
public interface IKyrolusAuthEventHandler<in TEvent> where TEvent : IKyrolusAuthEvent
{
    ValueTask HandleAsync(TEvent authEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// Central sink for publishing authentication events across the application.
/// </summary>
public interface IKyrolusAuthEventSink
{
    ValueTask PublishAsync<TEvent>(TEvent authEvent, CancellationToken cancellationToken = default) where TEvent : IKyrolusAuthEvent;
}

/// <summary>
/// Default implementation of <see cref="IKyrolusAuthEventSink"/> that resolves and invokes handlers safely.
/// </summary>
public sealed class KyrolusAuthEventDispatcher : IKyrolusAuthEventSink
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KyrolusAuthEventDispatcher> _logger;

    public KyrolusAuthEventDispatcher(IServiceProvider serviceProvider, ILogger<KyrolusAuthEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask PublishAsync<TEvent>(TEvent authEvent, CancellationToken cancellationToken = default) where TEvent : IKyrolusAuthEvent
    {
        ArgumentNullException.ThrowIfNull(authEvent);

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<IKyrolusAuthEventHandler<TEvent>> handlers;
        try
        {
            handlers = _serviceProvider.GetServices<IKyrolusAuthEventHandler<TEvent>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving event handlers for auth event '{EventType}'.", authEvent.EventType);
            return;
        }

        foreach (var handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await handler.HandleAsync(authEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing auth event handler '{HandlerType}' for event '{EventType}'.",
                    handler.GetType().Name, authEvent.EventType);
            }
        }
    }
}
