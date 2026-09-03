using System.Collections.Concurrent;

namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// In-memory <see cref="IKyrolusSagaStore"/> for testing and single-node applications. State is lost
/// on restart - a saga interrupted mid-flight cannot actually be resumed, since resuming depends on
/// the very state this store does not persist. Use a real store (backed by the application's
/// database) for anything where "the process restarted with a saga half-done" needs to recover.
/// </summary>
public sealed class InMemorySagaStore : IKyrolusSagaStore
{
    private readonly ConcurrentDictionary<Guid, KyrolusSagaInstance> _instances = new();

    /// <summary>Every instance currently held, for inspection in tests.</summary>
    public IReadOnlyCollection<KyrolusSagaInstance> AllInstances => _instances.Values.Select(Clone).ToArray();

    /// <inheritdoc />
    /// <remarks>
    /// Stores an independent snapshot, not the caller's own instance: <see cref="KyrolusSagaCoordinator"/>
    /// keeps mutating one <see cref="KyrolusSagaInstance"/> field by field across several statements
    /// before each <see cref="SaveAsync"/> call. Storing that same object by reference would let a
    /// concurrent <see cref="GetAsync"/>/<see cref="GetIncompleteAsync"/> observe it mid-mutation - a
    /// torn read where, say, <see cref="KyrolusSagaInstance.CurrentStepIndex"/> already advanced but
    /// <see cref="KyrolusSagaInstance.ContextJson"/> has not been rewritten yet - even though no
    /// <see cref="SaveAsync"/> call for that state actually completed yet.
    /// </remarks>
    public Task SaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[instance.Id] = Clone(instance);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<KyrolusSagaInstance?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
        => Task.FromResult(_instances.TryGetValue(sagaId, out var instance) ? Clone(instance) : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<KyrolusSagaInstance>> GetIncompleteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KyrolusSagaInstance> incomplete = _instances.Values
            .Where(instance => instance.Status is KyrolusSagaStatus.Running or KyrolusSagaStatus.Compensating)
            .Select(Clone)
            .ToList();
        return Task.FromResult(incomplete);
    }

    /// <summary>Clears every stored instance (test resets).</summary>
    public void Clear() => _instances.Clear();

    private static KyrolusSagaInstance Clone(KyrolusSagaInstance source) => new()
    {
        Id = source.Id,
        SagaName = source.SagaName,
        ContextJson = source.ContextJson,
        CurrentStepIndex = source.CurrentStepIndex,
        Status = source.Status,
        StartedAtUtc = source.StartedAtUtc,
        CompletedAtUtc = source.CompletedAtUtc,
        Error = source.Error,
        CorrelationId = source.CorrelationId
    };
}
