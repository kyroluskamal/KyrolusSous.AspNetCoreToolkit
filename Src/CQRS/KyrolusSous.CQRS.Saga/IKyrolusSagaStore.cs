namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Persists <see cref="KyrolusSagaInstance"/> state, so a saga survives a process restart.
/// </summary>
public interface IKyrolusSagaStore
{
    /// <summary>
    /// Creates or updates a saga instance's persisted state, gated by <see cref="KyrolusSagaInstance.Version"/>:
    /// the write only applies if <paramref name="instance"/>'s <see cref="KyrolusSagaInstance.Version"/>
    /// still matches what is currently stored for that <see cref="KyrolusSagaInstance.Id"/> (or, for a
    /// brand new id, that it is 0). On success the store bumps the stored version and updates
    /// <paramref name="instance"/>'s <see cref="KyrolusSagaInstance.Version"/> in place to match, so the
    /// same caller can keep saving the same object across several calls. On a version mismatch - another
    /// caller already wrote a newer version first - nothing is persisted and <paramref name="instance"/>
    /// is left untouched.
    /// </summary>
    /// <returns><see langword="true"/> if the write was applied; <see langword="false"/> if it lost the
    /// optimistic-concurrency race and was rejected.</returns>
    /// <remarks>
    /// A database-backed implementation maps this naturally onto a version/row-version column: an
    /// <c>UPDATE ... WHERE Id = @id AND Version = @expected</c> (or the equivalent <c>INSERT</c> for a
    /// new row) whose affected-row count reports whether the write applied.
    /// </remarks>
    Task<bool> SaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken = default);

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
