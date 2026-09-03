namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Persists <see cref="KyrolusSagaInstance"/> state, so a saga survives a process restart.
/// </summary>
public interface IKyrolusSagaStore
{
    /// <summary>Creates or updates a saga instance's persisted state.</summary>
    Task SaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a saga instance by id, or <see langword="null"/> if none exists.</summary>
    Task<KyrolusSagaInstance?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves every saga instance whose <see cref="KyrolusSagaInstance.Status"/> is
    /// <see cref="KyrolusSagaStatus.Running"/> or <see cref="KyrolusSagaStatus.Compensating"/> - the
    /// ones a restart left mid-flight and <see cref="IKyrolusSagaCoordinator.ResumeIncompleteAsync"/>
    /// should pick back up.
    /// </summary>
    Task<IReadOnlyList<KyrolusSagaInstance>> GetIncompleteAsync(CancellationToken cancellationToken = default);
}
