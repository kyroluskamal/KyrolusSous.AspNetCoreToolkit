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
    Task PublishAsync(IKyrolusNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously publishes a notification using a per-call publish strategy override.
    /// </summary>
    /// <param name="notification">The notification message object.</param>
    /// <param name="strategy">Optional publish strategy override for this call.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync(IKyrolusNotification notification, IKyrolusNotificationPublishStrategy? strategy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a notification that arrives as <see cref="object"/>, for callers that only have
    /// the runtime type - a queue consumer or an outbox drain, for example.
    /// </summary>
    /// <param name="notification">The notification. Must implement <see cref="IKyrolusNotification"/>.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <exception cref="ArgumentException">The object does not implement <see cref="IKyrolusNotification"/>.</exception>
    Task PublishAsync(object notification, CancellationToken cancellationToken = default);
}
