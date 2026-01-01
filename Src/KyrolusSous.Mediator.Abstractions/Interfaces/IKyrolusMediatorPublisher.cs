namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Defines a mechanism for publishing notification messages to multiple handlers.
/// </summary>
public interface IKyrolusMediatorPublisher
{
    /// <summary>
    /// Asynchronously publishes a notification to all relevant handlers.
    /// </summary>
    /// <param name="notification">The notification message object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously publishes a notification using a per-call publish strategy override.
    /// </summary>
    /// <param name="notification">The notification message object.</param>
    /// <param name="strategy">Optional publish strategy override for this call.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync(INotification notification, IKyrolusNotificationPublishStrategy? strategy, CancellationToken cancellationToken = default);
}
