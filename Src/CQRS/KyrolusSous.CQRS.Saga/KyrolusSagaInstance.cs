namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Where a running (or finished) saga stands: which saga it is, its serialized context, which step
/// it is on, and how it ended up if it did.
/// </summary>
public enum KyrolusSagaStatus
{
    /// <summary>Steps are still running forward.</summary>
    Running = 0,

    /// <summary>Every step completed. Terminal state.</summary>
    Completed = 1,

    /// <summary>A step failed; completed steps before it are being undone in reverse order.</summary>
    Compensating = 2,

    /// <summary>Every completed step was successfully undone. Terminal state - the saga's net effect is "as if it never ran".</summary>
    Compensated = 3,

    /// <summary>
    /// A compensation itself failed. Terminal only in the sense that nothing runs automatically from
    /// here - the saga is left in a partially-undone state that needs a human to look at it, or a
    /// manual call to <see cref="IKyrolusSagaCoordinator.RetryCompensationAsync"/> once the underlying
    /// problem (a downstream system being down, say) is fixed.
    /// </summary>
    Failed = 4
}

/// <summary>
/// A single saga's persisted state - enough to resume it (or its compensation) from scratch after a
/// crash, without re-running any step that already completed.
/// </summary>
public sealed class KyrolusSagaInstance
{
    /// <summary>Unique id of this saga run.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The <see cref="IKyrolusSagaDefinition.SagaName"/> this instance is running.</summary>
    public required string SagaName { get; init; }

    /// <summary>The saga's context, serialized by its definition.</summary>
    public required string ContextJson { get; set; }

    /// <summary>
    /// While <see cref="Status"/> is <see cref="KyrolusSagaStatus.Running"/>: the index of the next
    /// step to execute. While <see cref="KyrolusSagaStatus.Compensating"/> or
    /// <see cref="KyrolusSagaStatus.Failed"/>: the index one past the last step that still needs
    /// compensating (compensation walks backward from here).
    /// </summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>This saga's current status.</summary>
    public KyrolusSagaStatus Status { get; set; } = KyrolusSagaStatus.Running;

    /// <summary>When this saga was started.</summary>
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When this saga reached a terminal state, if it has.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>The error that triggered compensation, or that a compensation step itself raised.</summary>
    public string? Error { get; set; }

    /// <summary>Optional caller-supplied correlation id, for tracing this saga alongside the request that started it.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Optimistic-concurrency token. 0 for an instance never yet persisted; <see cref="IKyrolusSagaStore.SaveAsync"/>
    /// bumps it by one on every successful write and rejects a write whose <see cref="Version"/> does
    /// not match what is currently stored - two callers that both read the same version cannot both
    /// win, which is what stops the same saga instance being advanced (or compensated) twice in
    /// parallel.
    /// </summary>
    public int Version { get; set; }
}
