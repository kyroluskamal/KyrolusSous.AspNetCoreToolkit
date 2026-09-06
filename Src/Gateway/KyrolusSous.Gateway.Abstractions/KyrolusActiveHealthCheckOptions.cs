namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Configuration options for active destination health probing in a service cluster.
/// </summary>
public sealed record KyrolusActiveHealthCheckOptions
{
    /// <summary>
    /// Gets or sets whether active health probing is enabled for this cluster.
    /// Defaults to <c>true</c> when configured.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the time interval between health check probes sent to each destination.
    /// Defaults to 10 seconds.
    /// </summary>
    public TimeSpan? Interval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the timeout duration for each individual health probe request.
    /// Defaults to 5 seconds.
    /// </summary>
    public TimeSpan? Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the active health check policy algorithm (e.g., <c>"ConsecutiveFailures"</c>).
    /// </summary>
    public string? Policy { get; init; } = "ConsecutiveFailures";

    /// <summary>
    /// Gets or sets the HTTP URL path queried on each destination replica to verify health (e.g., <c>"/health"</c> or <c>"/healthz"</c>).
    /// </summary>
    public string? Path { get; init; } = "/health";
}
