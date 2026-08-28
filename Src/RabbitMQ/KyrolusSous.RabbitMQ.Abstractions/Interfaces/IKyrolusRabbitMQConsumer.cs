using KyrolusSous.RabbitMQ.Abstractions.Models;

namespace KyrolusSous.RabbitMQ.Abstractions.Interfaces;

/// <summary>
/// Defines a strongly-typed consumer for RabbitMQ messages.
/// </summary>
/// <typeparam name="TMessage">The payload message type.</typeparam>
public interface IKyrolusRabbitMQConsumer<in TMessage>
{
    /// <summary>
    /// Handles a consumed message.
    /// </summary>
    /// <param name="message">The deserialized message payload.</param>
    /// <param name="context">Metadata and delivery context for the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TMessage message, KyrolusRabbitMQConsumeContext context, CancellationToken cancellationToken = default);
}
