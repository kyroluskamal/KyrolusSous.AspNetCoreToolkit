namespace KyrolusSous.RabbitMQ.Abstractions.Interfaces;

/// <summary>
/// Defines a handler for domain and integration events consumed from RabbitMQ.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IKyrolusRabbitMQEventHandler<in TEvent>
{
    /// <summary>
    /// Handles the received event.
    /// </summary>
    /// <param name="event">The domain or integration event payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
