using RabbitMQ.Client;

namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

/// <summary>
/// Defines an abstraction over a managed RabbitMQ connection.
/// </summary>
public interface IKyrolusRabbitMQConnection : IDisposable
{
    IConnection Connection { get; }
    Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusRabbitMQConnection"/>.
/// </summary>
public interface IRabbitMQConnection : IKyrolusRabbitMQConnection
{
}
