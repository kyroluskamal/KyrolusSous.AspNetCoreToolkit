using System.Collections.Concurrent;
using KyrolusSous.Repositories.Marten.Abstractions.Upcasting;

namespace KyrolusSous.Repositories.Marten.Runtime.Upcasting;

/// <summary>
/// Default implementation of <see cref="IKyrolusMartenUpcastingPipeline"/>.
/// Recursively passes events through registered upcasters until reaching terminal schema.
/// </summary>
public sealed class KyrolusMartenUpcastingPipeline : IKyrolusMartenUpcastingPipeline
{
    private readonly ConcurrentDictionary<Type, IKyrolusMartenEventUpcaster> upcasterMap = new();

    public KyrolusMartenUpcastingPipeline(IEnumerable<IKyrolusMartenEventUpcaster>? upcasters = null)
    {
        if (upcasters is not null)
        {
            foreach (var upcaster in upcasters)
            {
                upcasterMap[upcaster.SourceEventType] = upcaster;
            }
        }
    }

    /// <summary>
    /// Registers an event upcaster at runtime.
    /// </summary>
    public void Register(IKyrolusMartenEventUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);
        upcasterMap[upcaster.SourceEventType] = upcaster;
    }

    public object Upcast(object @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var current = @event;
        var visited = new HashSet<Type>();

        while (upcasterMap.TryGetValue(current.GetType(), out var upcaster))
        {
            if (!visited.Add(current.GetType()))
            {
                throw new InvalidOperationException($"Cyclic event upcasting detected for type '{current.GetType().FullName}'.");
            }

            current = upcaster.Upcast(current);
        }

        return current;
    }

    public IReadOnlyList<object> UpcastRange(IEnumerable<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return [.. events.Select(Upcast)];
    }
}
