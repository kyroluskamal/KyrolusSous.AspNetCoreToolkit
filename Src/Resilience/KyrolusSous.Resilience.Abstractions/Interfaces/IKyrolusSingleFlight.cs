namespace KyrolusSous.Resilience;

/// <summary>
/// Thread-safe request coalescing (SingleFlight) mechanism to prevent Thundering Herd / Cache Stampede.
/// Guarantees that only one execution for a given key is in-flight at any time, sharing the result with all callers.
/// </summary>
public interface IKyrolusSingleFlight
{
    /// <summary>
    /// Executes the factory if no concurrent execution with the same key is active; otherwise awaits the active execution.
    /// </summary>
    Task<TResult> DoAsync<TResult>(
        string key,
        Func<CancellationToken, Task<TResult>> factory,
        CancellationToken cancellationToken = default);
}
