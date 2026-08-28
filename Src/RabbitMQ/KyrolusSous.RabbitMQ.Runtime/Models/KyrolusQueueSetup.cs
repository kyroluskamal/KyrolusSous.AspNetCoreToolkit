using KyrolusSous.RabbitMQ.Abstractions.Interfaces;

namespace KyrolusSous.RabbitMQ.Runtime.Models
{
    /// <summary>
    /// Defines queue setup and binding configuration for RabbitMQ.
    /// </summary>
    public class KyrolusQueueSetup : IKyrolusQueueSetup, global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IQueueSetup
    {
        public required string Name { get; set; }
        public required string RoutingKey { get; set; } = string.Empty;
        public bool Durable { get; set; } = true;
        public bool Exclusive { get; set; } = false;
        public bool Autodelete { get; set; } = false;
        public IDictionary<string, object?>? Arguments { get; set; } = null;
    }
}

namespace KyrolusSous.RabbitMQUtils.Models
{
    /// <summary>
    /// Backward-compatibility alias for <see cref="global::KyrolusSous.RabbitMQ.Runtime.Models.KyrolusQueueSetup"/>.
    /// </summary>
    public class QueueSetup : global::KyrolusSous.RabbitMQ.Runtime.Models.KyrolusQueueSetup
    {
    }
}
