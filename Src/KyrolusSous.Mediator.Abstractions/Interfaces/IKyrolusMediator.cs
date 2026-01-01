namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Unified mediator contract for sending requests and publishing notifications.
/// </summary>
public interface IKyrolusMediator
{
    /// <summary>Sends a query to a single handler.</summary>
    Task<TResponse> SendAsync<TResponse>(IKyrolusQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>Sends a request to a single handler.</summary>
    Task<TResponse> SendAsync<TResponse>(IKyrolusRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>Sends a command without a response.</summary>
    Task SendAsync(IKyrolusCommand command, CancellationToken cancellationToken = default);

    /// <summary>Sends a command with a response.</summary>
    Task<TResponse> SendAsync<TResponse>(IKyrolusCommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>Publishes a notification to all handlers.</summary>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);

    /// <summary>Publishes a notification using a per-call publish strategy override.</summary>
    Task PublishAsync(INotification notification, IKyrolusNotificationPublishStrategy? strategy, CancellationToken cancellationToken = default);

    /// <summary>Creates a response stream for a streaming request.</summary>
    IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IKyrolusStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}
