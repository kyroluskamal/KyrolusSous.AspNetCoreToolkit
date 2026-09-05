namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Safety limits for the transactional outbox pipeline.
/// </summary>
public static class KyrolusOutboxLimits
{
    /// <summary>
    /// Number of consecutive failures after which an outbox message stops being retried and
    /// transitions from <see cref="KyrolusOutboxMessageStatus.Failed"/> to
    /// <see cref="KyrolusOutboxMessageStatus.DeadLettered"/>. Without a terminal state, a message
    /// whose cause of failure is not transient (a bad payload, a permanently missing handler) would
    /// be retried by every processing pass forever, and would be indistinguishable from a message
    /// that failed once and will succeed next time. Once dead-lettered, the message stops consuming
    /// processing passes and becomes discoverable via <see cref="IKyrolusOutboxStore.GetDeadLetteredAsync"/>
    /// for an operator to inspect and, once the underlying cause is fixed, recover with
    /// <see cref="IKyrolusOutboxStore.RequeueAsync"/>.
    /// </summary>
    public const int MaxRetryCount = 5;

    /// <summary>
    /// Upper bound on the <c>batchSize</c> a caller may request from <see cref="IKyrolusOutboxStore.GetPendingAsync"/>,
    /// <see cref="IKyrolusOutboxStore.GetDeadLetteredAsync"/>, or <see cref="KyrolusOutboxProcessor.ProcessPendingMessagesAsync"/>.
    /// Unlike <c>KyrolusPagingLimits.MaxPageSize</c> elsewhere in this codebase, these methods
    /// previously accepted an unclamped caller-supplied value - a misconfigured or malicious caller
    /// requesting an unbounded batch could pull the entire outbox table into memory in one call. 500
    /// is generous enough for normal processing-pass sizes while keeping a single pass bounded.
    /// </summary>
    public const int MaxBatchSize = 500;

    /// <summary>
    /// Base (in seconds) of the exponential backoff delay applied before a <see cref="KyrolusOutboxMessageStatus.Failed"/>
    /// message becomes claimable again, computed as <c>BackoffBaseSeconds ^ RetryCount</c> (2s, 4s, 8s,
    /// 16s, ... doubling with every consecutive failure).
    /// </summary>
    /// <remarks>
    /// Without a backoff, a message that fails against a genuinely transient condition (a downstream
    /// dependency's brief outage, a momentary network blip) is retried again on the very next
    /// processing-pass tick - potentially seconds later - and again after that, hammering the same
    /// failing dependency at full processing frequency instead of giving it time to recover. Doubling
    /// the delay after each consecutive failure backs off quickly from a dependency that stays down
    /// while still recovering promptly (2 seconds) from a single blip.
    /// </remarks>
    public const int BackoffBaseSeconds = 2;

    /// <summary>
    /// Upper bound on the computed backoff delay, regardless of how large <c>BackoffBaseSeconds ^
    /// RetryCount</c> grows.
    /// </summary>
    /// <remarks>
    /// Unbounded exponential growth would, for a message that has failed several times but not yet
    /// reached <see cref="MaxRetryCount"/> (e.g. a larger, application-configured retry ceiling than the
    /// default), push the next retry hours or days into the future - effectively abandoning a message
    /// long before its own <see cref="MaxRetryCount"/>-driven <see cref="KyrolusOutboxMessageStatus.DeadLettered"/>
    /// transition would ever kick in. Five minutes keeps the delay meaningful for a sustained outage
    /// without silently starving retries for messages still short of dead-lettering.
    /// </remarks>
    public static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromMinutes(5);
}
