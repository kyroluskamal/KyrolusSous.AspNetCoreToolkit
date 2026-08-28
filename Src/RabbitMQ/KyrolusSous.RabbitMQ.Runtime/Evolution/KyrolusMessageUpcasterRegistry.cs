using System.Collections.Concurrent;
using KyrolusSous.RabbitMQ.Abstractions.Evolution;

namespace KyrolusSous.RabbitMQ.Runtime.Evolution;

/// <summary>
/// Registry and transformation engine for message schema upcasters.
/// </summary>
public class KyrolusMessageUpcasterRegistry
{
    private readonly ConcurrentDictionary<Type, IKyrolusMessageUpcaster> _upcasters = new();

    public KyrolusMessageUpcasterRegistry Register<TOld, TNew>(IKyrolusMessageUpcaster<TOld, TNew> upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);
        _upcasters[typeof(TOld)] = upcaster;
        return this;
    }

    public KyrolusMessageUpcasterRegistry Register(IKyrolusMessageUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);
        _upcasters[upcaster.SourceType] = upcaster;
        return this;
    }

    /// <summary>
    /// Upcasts a message instance through registered upcasters until no further upcasters match or target type is reached.
    /// </summary>
    public object Upcast(object message, Type? targetType = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var current = message;
        while (_upcasters.TryGetValue(current.GetType(), out var upcaster))
        {
            current = upcaster.Upcast(current);
            if (targetType != null && current.GetType() == targetType)
            {
                break;
            }
        }

        return current;
    }
}
