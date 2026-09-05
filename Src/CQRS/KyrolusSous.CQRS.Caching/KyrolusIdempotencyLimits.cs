namespace KyrolusSous.CQRS.Caching;

/// <summary>
/// Safety limits and defaults for the idempotency pipeline.
/// </summary>
public static class KyrolusIdempotencyLimits
{
    /// <summary>
    /// TTL applied to a freshly-claimed idempotency key while its handler is still running - short
    /// by design, and deliberately distinct from the full <c>IdempotencyTtl</c> written once the
    /// handler completes (see <see cref="KyrolusIdempotencyBehavior{TRequest,TResponse}"/>).
    /// </summary>
    /// <remarks>
    /// A claim exists only to block a genuinely-concurrent duplicate while the handler is in flight.
    /// If cancellation abandons a claim before it is ever released or completed (the
    /// <c>OperationCanceledException</c> branch deliberately leaves it in place, since the handler may
    /// already have done its real work by then), leaving that claim under the full TTL would block
    /// every retry with the same key for up to 24 hours even though nothing is actually running
    /// anymore. A short claim TTL lets an abandoned claim expire and become retryable again quickly,
    /// while still correctly rejecting a duplicate that arrives within the window.
    /// <para>
    /// A short TTL alone is not sufficient, though: a fixed value with no renewal means a
    /// legitimately slow handler that runs longer than this TTL has its claim expire WHILE STILL
    /// EXECUTING, letting a duplicate that arrives in that window claim the key and run the handler a
    /// second time concurrently - the exact double-execution this behavior exists to prevent, and a
    /// worse failure mode than the original "fixed long TTL blocks retries for 24h" bug this value was
    /// introduced to fix. <see cref="KyrolusIdempotencyBehavior{TRequest,TResponse}"/> resolves both
    /// problems together by treating this value as short-lived by design but periodically RENEWING it
    /// (see <see cref="DefaultClaimRenewalInterval"/>) for as long as the handler is genuinely still
    /// running, and letting renewal stop - so the claim naturally expires - the moment the handler
    /// returns, throws, or the process crashes.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan InProgressClaimTtl = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Default interval between periodic re-extensions of an in-progress claim's TTL while its handler
    /// is still running.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="InProgressClaimTtl"/> (half of it) rather than being an unrelated fixed
    /// value, so the two stay in lockstep if <see cref="InProgressClaimTtl"/> is ever changed. Renewing
    /// at half the claim TTL leaves a full extra renewal attempt as a safety margin before the claim
    /// would actually expire - a single missed or slow renewal tick (a transient cache-provider hiccup)
    /// still leaves time for the next one to land before the claim lapses.
    /// </remarks>
    public static readonly TimeSpan DefaultClaimRenewalInterval = TimeSpan.FromTicks(InProgressClaimTtl.Ticks / 2);
}
