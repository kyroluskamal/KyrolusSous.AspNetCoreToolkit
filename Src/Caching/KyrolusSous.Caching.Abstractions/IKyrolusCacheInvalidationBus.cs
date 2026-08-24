namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines a lightweight pub/sub messaging bus used to broadcast cache eviction events across all nodes in a cluster.
/// </summary>
/// <remarks>
/// <b>Real-World Use Case (Multi-Level Hybrid Caching / Near Cache):</b>
/// Suppose you run a high-traffic e-commerce store with 10 web servers. 
/// Each server keeps frequently accessed products in local RAM (L1 memory cache for microsecond reads).
/// When an admin updates a product on Server 1, Server 1 updates SQL database and Redis, and then publishes an 
/// invalidation message through this bus. Servers 2 through 10 receive the message in real-time and immediately 
/// evict the stale product from their local RAM, ensuring customers never see outdated prices on any server.
/// </remarks>
public interface IKyrolusCacheInvalidationBus
{
    /// <summary>
    /// Asynchronously broadcasts a cache invalidation event to all cluster nodes.
    /// </summary>
    /// <param name="message">The invalidation payload identifying the evicted key, tag, or pattern.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(KyrolusCacheInvalidationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes a listener delegate to handle incoming invalidation events broadcast by other cluster nodes.
    /// </summary>
    /// <param name="handler">The callback executed when an invalidation notification arrives.</param>
    /// <returns>An <see cref="IDisposable"/> subscription token.</returns>
    IDisposable Subscribe(Func<KyrolusCacheInvalidationMessage, Task> handler);
}
