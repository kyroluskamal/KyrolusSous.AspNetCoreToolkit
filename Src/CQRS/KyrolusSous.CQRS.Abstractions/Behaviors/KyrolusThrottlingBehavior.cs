namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Holds the semaphores <see cref="KyrolusThrottlingBehavior{TRequest, TResponse}"/> throttles on.
/// </summary>
/// <remarks>
/// Kept on a non-generic type deliberately: a <see langword="static"/> field on a generic class gets
/// its own storage per closed instantiation (one dictionary per distinct (TRequest, TResponse) pair),
/// which would defeat <see cref="IThrottledRequest.ThrottleKey"/> the moment two different request
/// types shared a key - each request type would silently get its own semaphore instead of contending
/// for the same one. <c>KyrolusSous.Mediator.Runtime.Implementations.KyrolusMediatorMetrics</c>
/// documents avoiding the exact same pitfall for the same reason.
/// </remarks>
internal static class KyrolusThrottlingSemaphores
{
    private sealed record Entry(SemaphoreSlim Semaphore, int MaxConcurrency);

    /// <summary>
    /// Hard cap on distinct <see cref="IThrottledRequest.ThrottleKey"/> values tracked at once.
    /// </summary>
    /// <remarks>
    /// Without a cap this dictionary grew by one <see cref="SemaphoreSlim"/> forever - entries were
    /// never removed, so a workload that throttles on a per-user or per-tenant key (rather than a
    /// fixed, small set of keys) leaked memory slowly but without bound. Past the cap,
    /// <see cref="GetOrAdd"/> evicts currently-idle entries (no caller holding a permit right now) to
    /// make room; a key that's genuinely busy is never touched, and a key evicted while idle just
    /// gets a fresh semaphore on its next call - resetting only that one key's own bookkeeping.
    /// </remarks>
    private const int MaxTrackedKeys = 10_000;

    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    internal static SemaphoreSlim GetOrAdd(string key, int maxConcurrency)
    {
        var entry = Entries.GetOrAdd(
            key,
            static (_, mc) => new Entry(new SemaphoreSlim(mc, mc), mc),
            maxConcurrency);

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

            // Only an entry nobody currently holds a permit for (CurrentCount == MaxConcurrency) is
            // safe to drop.
            if (kv.Value.Semaphore.CurrentCount != kv.Value.MaxConcurrency) continue;

            // Deliberately NOT calling Semaphore.Dispose(): a caller could have already fetched this
            // exact instance via GetOrAdd an instant before this removal takes effect and be about to
            // WaitAsync/Release on it - disposing here would throw ObjectDisposedException on what
            // should be a perfectly normal request. Dropping the dictionary entry is enough: once
            // every holder of the reference finishes with it, it becomes ordinary garbage - the
            // unbounded dictionary, not the SemaphoreSlim's lifetime, was the actual leak.
            Entries.TryRemove(new KeyValuePair<string, Entry>(kv.Key, kv.Value));
        }
    }

    internal static void ClearSemaphores() => Entries.Clear();

    /// <summary>Test hook: number of distinct throttle keys currently tracked.</summary>
    internal static int TrackedKeyCount => Entries.Count;
}

/// <summary>
/// Pipeline behavior providing concurrency limiting and throttling on requests implementing <see cref="IThrottledRequest"/>.
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

        if (request is not IThrottledRequest throttled)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var throttleKey = string.IsNullOrWhiteSpace(throttled.ThrottleKey)
            ? typeof(TRequest).FullName ?? typeof(TRequest).Name
            : throttled.ThrottleKey.Trim();
        var maxConcurrency = throttled.MaxConcurrentExecutions <= 0 ? 5 : throttled.MaxConcurrentExecutions;
        var timeout = throttled.ThrottleTimeout;

        var semaphore = KyrolusThrottlingSemaphores.GetOrAdd(throttleKey, maxConcurrency);

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
