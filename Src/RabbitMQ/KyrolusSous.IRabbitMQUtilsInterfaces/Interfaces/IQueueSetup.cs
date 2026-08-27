namespace KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

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

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusQueueSetup"/>.
/// </summary>
public interface IQueueSetup : IKyrolusQueueSetup
{
}
