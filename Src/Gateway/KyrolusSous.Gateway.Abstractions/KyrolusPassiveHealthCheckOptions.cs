namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Configuration options for passive destination health observation in a service cluster.
/// Reacts to runtime HTTP response codes and transport errors during client request forwarding.
/// </summary>
public sealed record KyrolusPassiveHealthCheckOptions
{
    /// <summary>
    /// Gets or sets whether passive health monitoring is enabled.
    /// Defaults to <c>true</c> when configured.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the passive health check policy algorithm (e.g., <c>"TransportFailureRate"</c>).
    /// </summary>
    public string? Policy { get; init; } = "TransportFailureRate";

    /// <summary>
    /// Gets or sets the quarantine duration before an unhealthy destination is automatically reactivated and retried.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan? ReactivationPeriod { get; init; } = TimeSpan.FromSeconds(30);
}
