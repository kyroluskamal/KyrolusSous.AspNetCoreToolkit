namespace KyrolusSous.Repositories.Marten.Abstractions.Upcasting;

/// <summary>
/// Non-generic contract for transforming legacy event types into newer schema versions.
/// </summary>
public interface IKyrolusMartenEventUpcaster
{
    /// <summary>
    /// The source event type to be upcasted.
    /// </summary>
    Type SourceEventType { get; }

    /// <summary>
    /// The target event type produced by the upcaster.
    /// </summary>
    Type TargetEventType { get; }

    /// <summary>
    /// Transforms the source event object into the target event object.
    /// </summary>
    object Upcast(object sourceEvent);
}

/// <summary>
/// Strongly-typed contract for evolving domain events from <typeparamref name="TSource"/> to <typeparamref name="TTarget"/>.
/// </summary>
public abstract class KyrolusMartenEventUpcasterBase<TSource, TTarget> : IKyrolusMartenEventUpcaster
    where TSource : class
    where TTarget : class
{
    public Type SourceEventType => typeof(TSource);
    public Type TargetEventType => typeof(TTarget);

    public object Upcast(object sourceEvent)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);
        if (sourceEvent is TSource typedSource)
        {
            return Upcast(typedSource);
        }

        throw new ArgumentException($"Event of type '{sourceEvent.GetType().FullName}' is not compatible with upcaster source type '{typeof(TSource).FullName}'.", nameof(sourceEvent));
    }

    /// <summary>
    /// Strongly-typed upcast method to be implemented by domain upcasters.
    /// </summary>
    public abstract TTarget Upcast(TSource sourceEvent);
}

/// <summary>
/// Upcasting pipeline responsible for migrating events through successive schema iterations.
/// </summary>
public interface IKyrolusMartenUpcastingPipeline
{
    /// <summary>
    /// Upcasts a single event through all registered upcasters until reaching the latest schema.
    /// </summary>
    object Upcast(object @event);

    /// <summary>
    /// Upcasts a sequence of events.
    /// </summary>
    IReadOnlyList<object> UpcastRange(IEnumerable<object> events);
}
