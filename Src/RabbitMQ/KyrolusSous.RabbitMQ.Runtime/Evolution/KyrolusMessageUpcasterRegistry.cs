using System.Collections.Concurrent;
using KyrolusSous.RabbitMQ.Abstractions.Evolution;

namespace KyrolusSous.RabbitMQ.Runtime.Evolution;

/// <summary>
/// Registry and transformation engine for message schema upcasters with circular dependency detection.
/// </summary>
public class KyrolusMessageUpcasterRegistry
{
    private readonly ConcurrentDictionary<Type, IKyrolusMessageUpcaster> _upcasters = new();
    private const int MaxUpcastDepth = 50;

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
        var visitedTypes = new HashSet<Type> { current.GetType() };
        int depth = 0;

        while (_upcasters.TryGetValue(current.GetType(), out var upcaster))
        {
            if (++depth > MaxUpcastDepth)
            {
                throw new InvalidOperationException($"Maximum upcasting depth of {MaxUpcastDepth} exceeded.");
            }

            current = upcaster.Upcast(current);
            var nextType = current.GetType();

            if (!visitedTypes.Add(nextType))
            {
                throw new InvalidOperationException($"Circular schema upcasting loop detected for type {nextType.FullName}.");
            }

            if (targetType != null && nextType == targetType)
            {
                break;
            }
        }

        return current;
    }
}
