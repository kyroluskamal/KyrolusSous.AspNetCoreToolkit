namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Specifies the Redis node role targeted for server-level operations (such as SCAN or health checks).
/// </summary>
public enum KyrolusRedisServerRole
{
    /// <summary>
    /// Targets any available online Redis server node.
    /// </summary>
    Any = 0,

    /// <summary>
    /// Targets only master / primary Redis nodes.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Pattern removals and write operations must be directed to primary nodes.
    /// </remarks>
    Primary = 1,

    /// <summary>
    /// Targets only read-only replica / slave Redis nodes.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Read Scaling):</b>
    /// Offloading heavy SCAN queries and read operations to replicas to avoid CPU load on the write master.
    /// </remarks>
    Replica = 2
}
