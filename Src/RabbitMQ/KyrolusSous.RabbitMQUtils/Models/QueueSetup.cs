using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;

namespace KyrolusSous.RabbitMQUtils.Models;

/// <summary>
/// Defines queue setup and binding configuration.
/// </summary>
public class KyrolusQueueSetup : IKyrolusQueueSetup, IQueueSetup
{
    public required string Name { get; set; }
    public required string RoutingKey { get; set; } = string.Empty;
    public bool Durable { get; set; } = true;
    public bool Exclusive { get; set; } = false;
    public bool Autodelete { get; set; } = false;
    public IDictionary<string, object?>? Arguments { get; set; } = null;
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusQueueSetup"/>.
/// </summary>
public class QueueSetup : KyrolusQueueSetup
{
}
