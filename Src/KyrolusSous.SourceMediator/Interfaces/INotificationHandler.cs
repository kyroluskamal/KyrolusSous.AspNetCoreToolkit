namespace KyrolusSous.SourceMediator.Interfaces;

/// <summary>
/// Defines a handler for a specific notification type.
/// A single notification can be handled by multiple handlers.
/// </summary>
/// <typeparam name="TNotification">The type of notification being handled.</typeparam>
// TNotification is contravariant (in) as it's consumed by the handler.
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>
    /// Handles the notification.
    /// </summary>
    /// <param name="notification">The notification message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
