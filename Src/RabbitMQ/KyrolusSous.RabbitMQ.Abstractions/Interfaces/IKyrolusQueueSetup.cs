namespace KyrolusSous.RabbitMQ.Abstractions.Interfaces
{
    /// <summary>
    /// Defines queue setup and binding configuration for RabbitMQ.
    /// </summary>
    public interface IKyrolusQueueSetup
    {
        string Name { get; set; }
        string RoutingKey { get; set; }
        bool Durable { get; set; }
        bool Exclusive { get; set; }
        bool Autodelete { get; set; }
        IDictionary<string, object?>? Arguments { get; set; }
    }
}

namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusQueueSetup"/>.
    /// </summary>
    public interface IKyrolusQueueSetup : global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusQueueSetup
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Abstractions.Interfaces.IKyrolusQueueSetup"/>.
    /// </summary>
    public interface IQueueSetup : IKyrolusQueueSetup
    {
    }
}
