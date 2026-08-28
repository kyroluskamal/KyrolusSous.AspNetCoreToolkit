namespace KyrolusSous.RabbitMQ.Abstractions.Interfaces
{
    /// <summary>
    /// Core listener interface for consuming messages from RabbitMQ queues.
    /// </summary>
    public interface IKyrolusRabbitMqListener
    {
        Task ConsumeAsync<TEvent>(string queue, Func<TEvent, Task> action, bool durable = true, bool exclusive = false, bool autoDelete = false, bool autoAck = true, IDictionary<string, object?>? arguments = null);
        Task ConsumeWithContextAsync<TEvent>(string queue, Func<TEvent, IDictionary<string, object?>?, Task> action, bool durable = true, bool exclusive = false, bool autoDelete = false, bool autoAck = false, IDictionary<string, object?>? arguments = null);
    }
}

namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMqListener"/>.
    /// </summary>
    public interface IKyrolusRabbitMqListener : global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMqListener
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMqListener"/>.
    /// </summary>
    public interface IRabbitMqListener : IKyrolusRabbitMqListener
    {
    }
}
