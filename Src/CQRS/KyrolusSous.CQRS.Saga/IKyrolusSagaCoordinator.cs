namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Runs sagas: executes their steps in order, compensates completed steps in reverse if one fails,
/// and can pick a saga back up after a crash.
/// </summary>
public interface IKyrolusSagaCoordinator
{
    /// <summary>
    /// Starts a new saga: persists its initial state, then runs its steps in order until one fails
    /// or all of them complete.
    /// </summary>
    /// <param name="definition">The saga to run.</param>
    /// <param name="context">The saga's initial shared state.</param>
    /// <param name="cancellationToken">Cancels the run. Does not cancel <paramref name="correlationId"/>'s claim.</param>
    /// <param name="correlationId">
    /// Optional caller-supplied id that makes starting this saga idempotent: a call that reuses an id
    /// already seen returns the existing instance's <see cref="Guid"/> instead of starting a second,
    /// independent saga for what should be one logical operation - a caller retry after a timeout on
    /// an already-successful call, or at-least-once redelivery from a queue, would otherwise do
    /// exactly that. <see langword="null"/> (the default) opts out entirely, matching every call site
    /// written before this parameter existed.
    /// </param>
    /// <returns>The id of the new saga instance, or of the instance <paramref name="correlationId"/>
    /// already identified. Check <see cref="IKyrolusSagaStore.GetAsync"/> with it afterward to see
    /// whether the saga completed, was compensated, or needs attention.</returns>
    Task<Guid> StartAsync<TContext>(
        KyrolusSagaDefinition<TContext> definition,
        TContext context,
        CancellationToken cancellationToken = default,
        string? correlationId = null)
        where TContext : class;

    /// <summary>
    /// Finds every saga instance left <see cref="KyrolusSagaStatus.Running"/> or
    /// <see cref="KyrolusSagaStatus.Compensating"/> (typically: the process restarted while they were
    /// mid-flight) and resumes each one from where it left off - no step that already completed runs
    /// again.
    /// </summary>
    /// <returns>How many instances were resumed.</returns>
    Task<int> ResumeIncompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries compensation for a saga left in <see cref="KyrolusSagaStatus.Failed"/> - a compensation
    /// step itself threw, and nothing runs automatically for a failed saga until this is called (most
    /// often once whatever the compensation step depends on is fixed).
    /// </summary>
    Task RetryCompensationAsync(Guid sagaId, CancellationToken cancellationToken = default);
}
