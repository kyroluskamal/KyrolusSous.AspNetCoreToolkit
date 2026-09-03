namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// The non-generic handle <see cref="IKyrolusSagaCoordinator"/> drives a saga through.
/// </summary>
/// <remarks>
/// The coordinator has to be able to run and resume a saga without knowing its
/// <c>TContext</c> type - it only ever has a saga name (read back from a stored
/// <see cref="KyrolusSagaInstance"/>) and a JSON blob. This interface is what makes that possible:
/// <see cref="KyrolusSagaDefinition{TContext}"/> implements it by closing over its own
/// <c>TContext</c> internally, so the coordinator only ever deals with <c>object</c> and this
/// interface, never with a generic saga type it would have to resolve from a bare string.
/// Application code should derive from <see cref="KyrolusSagaDefinition{TContext}"/> rather than
/// implementing this directly.
/// </remarks>
public interface IKyrolusSagaDefinition
{
    /// <summary>
    /// A unique, stable name for this saga. Stored on every <see cref="KyrolusSagaInstance"/> it
    /// starts, and used to find this definition again when resuming after a restart - renaming it
    /// orphans any saga instance already in flight under the old name.
    /// </summary>
    string SagaName { get; }

    /// <summary>The number of steps in this saga, in execution order.</summary>
    int StepCount { get; }

    /// <summary>Serializes a context instance for storage.</summary>
    string SerializeContext(object context);

    /// <summary>Deserializes a context instance previously produced by <see cref="SerializeContext"/>.</summary>
    object DeserializeContext(string json);

    /// <summary>Runs the forward action of the step at <paramref name="stepIndex"/>.</summary>
    Task ExecuteStepAsync(int stepIndex, object context, CancellationToken cancellationToken);

    /// <summary>Runs the compensating action of the step at <paramref name="stepIndex"/>.</summary>
    Task CompensateStepAsync(int stepIndex, object context, CancellationToken cancellationToken);
}
