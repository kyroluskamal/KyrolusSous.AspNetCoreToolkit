using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Abstractions.Interfaces
{
    /// <summary>
    /// Defines an abstraction over a managed RabbitMQ connection.
    /// </summary>
    public interface IKyrolusRabbitMQConnection : IDisposable
    {
        IConnection Connection { get; }
        Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);
    }
}

namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQConnection"/>.
    /// </summary>
    public interface IKyrolusRabbitMQConnection : global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQConnection
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusRabbitMQConnection"/>.
    /// </summary>
    public interface IRabbitMQConnection : IKyrolusRabbitMQConnection
    {
    }
}
