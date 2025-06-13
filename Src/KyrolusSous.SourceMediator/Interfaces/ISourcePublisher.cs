namespace KyrolusSous.SourceMediator.Interfaces;

/// <summary>
/// Defines a mechanism for publishing notification messages to multiple handlers.
/// </summary>
public interface ISourcePublisher
{
    /// <summary>
    /// Asynchronously publishes a notification to all relevant handlers.
    /// </summary>
    /// <param name="notification">The notification message object.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync(INotification notification, CancellationToken cancellationToken = default);
}