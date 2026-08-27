namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

/// <summary>
/// Core listener interface for consuming messages from RabbitMQ queues.
/// </summary>
public interface IKyrolusRabbitMqListener
{
    Task ConsumeAsync<TEvent>(string queue, Func<TEvent, Task> action, bool durable = true, bool exclusive = false, bool autoDelete = false, bool autoAck = true, IDictionary<string, object?>? arguments = null);
    Task ConsumeWithContextAsync<TEvent>(string queue, Func<TEvent, IDictionary<string, object?>?, Task> action, bool durable = true, bool exclusive = false, bool autoDelete = false, bool autoAck = false, IDictionary<string, object?>? arguments = null);
}

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusRabbitMqListener"/>.
/// </summary>
public interface IRabbitMqListener : IKyrolusRabbitMqListener
{
}
