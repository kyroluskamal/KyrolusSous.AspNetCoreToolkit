namespace KyrolusSous.Repositories.Marten.Runtime.Projection;

/// <summary>
/// Options to control Marten projection daemon startup and freshness checks.
/// </summary>
public sealed class KyrolusMartenDaemonOptions
{
    /// <summary>
    /// Optional customization of <see cref="DaemonSettings"/> before the daemon is built.
    /// </summary>
    public Action<object>? ConfigureSettings { get; set; }

    /// <summary>
    /// Start shards automatically. If false the daemon is created only.
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Optional list of shard names to start; if empty starts all shards.
    /// </summary>
    public IReadOnlyList<string>? ShardsToStart { get; set; }

    /// <summary>
    /// Optionally rebuild these projections after daemon creation.
    /// </summary>
    public IReadOnlyList<string>? RebuildProjections { get; set; }

    /// <summary>
    /// Timeout for waiting on non-stale data; null means no timeout.
    /// </summary>
    public TimeSpan? WaitForNonStaleTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
