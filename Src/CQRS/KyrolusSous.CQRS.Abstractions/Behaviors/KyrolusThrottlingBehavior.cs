namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Holds the semaphores <see cref="KyrolusThrottlingBehavior{TRequest, TResponse}"/> throttles on.
/// </summary>
/// <remarks>
/// Kept on a non-generic type deliberately: a <see langword="static"/> field on a generic class gets
/// its own storage per closed instantiation (one dictionary per distinct (TRequest, TResponse) pair),
/// which would defeat <see cref="IKyrolusThrottledRequest.ThrottleKey"/> the moment two different request
/// types shared a key - each request type would silently get its own semaphore instead of contending
/// for the same one. <c>KyrolusSous.Mediator.Runtime.Implementations.KyrolusMediatorMetrics</c>
/// documents avoiding the exact same pitfall for the same reason.
/// </remarks>
internal static class KyrolusThrottlingSemaphores
{
    private sealed record Entry(SemaphoreSlim Semaphore, int MaxConcurrency);

    /// <summary>
    /// Hard cap on distinct <see cref="IKyrolusThrottledRequest.ThrottleKey"/> values tracked at once.
    /// </summary>
    /// <remarks>
    /// Without a cap this dictionary grew by one <see cref="SemaphoreSlim"/> forever - entries were
    /// never removed, so a workload that throttles on a per-user or per-tenant key (rather than a
    /// fixed, small set of keys) leaked memory slowly but without bound. Past the cap,
    /// <see cref="GetOrAdd(string, int, out int)"/> evicts currently-idle entries (no caller holding a permit right now) to
    /// make room; a key that's genuinely busy is never touched, and a key evicted while idle just
    /// gets a fresh semaphore on its next call - resetting only that one key's own bookkeeping.
    /// </remarks>
    private const int MaxTrackedKeys = 10_000;

    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    internal static SemaphoreSlim GetOrAdd(string key, int maxConcurrency) => GetOrAdd(key, maxConcurrency, out _);

    internal static SemaphoreSlim GetOrAdd(string key, int maxConcurrency, out int actualMaxConcurrency)
    {
        var entry = Entries.GetOrAdd(
            key,
            static (_, mc) => new Entry(new SemaphoreSlim(mc, mc), mc),
            maxConcurrency);

        actualMaxConcurrency = entry.MaxConcurrency;

        if (Entries.Count > MaxTrackedKeys)
        {
            EvictIdle();
        }

        return entry.Semaphore;
    }

    private static void EvictIdle()
    {
        foreach (var kv in Entries)
        {
            if (Entries.Count <= MaxTrackedKeys) break;

            // Cheap pre-filter: skip entries that are obviously still busy without paying for the
            // acquire-all attempt below.
            if (kv.Value.Semaphore.CurrentCount != kv.Value.MaxConcurrency) continue;

            // Prove idleness instead of just sampling it: momentarily acquiring every permit means no
            // other caller can be mid-WaitAsync/holding one at the same time, so removing the entry
            // while we hold them all cannot let the tracked concurrency exceed MaxConcurrency. A plain
            // CurrentCount read-then-remove has a gap where a WaitAsync between the two steps would be
            // orphaned onto an entry that's about to disappear.
            if (!TryAcquireAllPermits(kv.Value.Semaphore, kv.Value.MaxConcurrency, out var acquired)) continue;

            try
            {
                Entries.TryRemove(new KeyValuePair<string, Entry>(kv.Key, kv.Value));
            }
            finally
            {
                // Deliberately NOT calling Semaphore.Dispose(): a caller could already hold a
                // reference to this exact instance (fetched via GetOrAdd moments before removal) and
                // be about to WaitAsync/Release on it once we hand the permits back - disposing here
                // would throw ObjectDisposedException on what should be a perfectly normal request.
                // Dropping the dictionary entry is enough: once every holder of the reference finishes
                // with it, it becomes ordinary garbage - the unbounded dictionary, not the
                // SemaphoreSlim's lifetime, was the actual leak.
                for (var i = 0; i < acquired; i++)
                {
                    kv.Value.Semaphore.Release();
                }
            }
        }
    }

    private static bool TryAcquireAllPermits(SemaphoreSlim semaphore, int maxConcurrency, out int acquired)
    {
        acquired = 0;
        while (acquired < maxConcurrency && semaphore.Wait(0))
        {
            acquired++;
        }

        if (acquired == maxConcurrency) return true;

        for (var i = 0; i < acquired; i++)
        {
            semaphore.Release();
        }
        acquired = 0;
        return false;
    }

    internal static void ClearSemaphores() => Entries.Clear();

    /// <summary>Test hook: number of distinct throttle keys currently tracked.</summary>
    internal static int TrackedKeyCount => Entries.Count;
}

/// <summary>
/// Pipeline behavior providing concurrency limiting and throttling on requests implementing <see cref="IKyrolusThrottledRequest"/>.
/// </summary>
[PipelineOrder(-750)]
public sealed class KyrolusThrottlingBehavior<TRequest, TResponse>(
    ILogger<KyrolusThrottlingBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IKyrolusThrottledRequest throttled)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var throttleKey = string.IsNullOrWhiteSpace(throttled.ThrottleKey)
            ? typeof(TRequest).FullName ?? typeof(TRequest).Name
            : throttled.ThrottleKey.Trim();
        var maxConcurrency = throttled.MaxConcurrentExecutions <= 0 ? 5 : throttled.MaxConcurrentExecutions;
        var timeout = throttled.ThrottleTimeout;

        var semaphore = KyrolusThrottlingSemaphores.GetOrAdd(throttleKey, maxConcurrency, out var actualMaxConcurrency);

        if (actualMaxConcurrency != maxConcurrency)
        {
            // The semaphore for this key was already created (by an earlier caller, possibly a
            // different request type sharing the same ThrottleKey) with a different MaxConcurrency.
            // The dictionary tracks one semaphore per key, so this request's value is silently ignored
            // in favor of whichever value got there first - surfacing that here at least makes the
            // mismatch observable instead of a silent surprise.
            _logger?.LogWarning(
                "[Kyrolus CQRS] Throttling key '{ThrottleKey}' is already tracked with MaxConcurrentExecutions={ExistingMax}; this request's MaxConcurrentExecutions={RequestedMax} is ignored for the shared semaphore.",
                throttleKey,
                actualMaxConcurrency,
                maxConcurrency);
        }

        var acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (!acquired)
        {
            _logger?.LogWarning(
                "[Kyrolus CQRS] Throttling rejected request {RequestType} for key '{ThrottleKey}'. Exceeded limit of {MaxConcurrency} within timeout {TimeoutMs}ms",
                typeof(TRequest).Name,
                throttleKey,
                maxConcurrency,
                timeout.TotalMilliseconds);

            throw new TimeoutException($"[Kyrolus CQRS] Request '{typeof(TRequest).Name}' was throttled. Concurrency limit of {maxConcurrency} reached for key '{throttleKey}'.");
        }

        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Clears cached semaphores (useful for test resets and memory reclamation).
    /// </summary>
    public static void ClearSemaphores() => KyrolusThrottlingSemaphores.ClearSemaphores();
}
