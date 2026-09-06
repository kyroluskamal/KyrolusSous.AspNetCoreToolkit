namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Encapsulates active and passive health check monitoring policies for a service cluster.
/// Ensures traffic is automatically diverted away from unhealthy or crashed destination replicas.
/// </summary>
public sealed record KyrolusHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the active health check probing options.
    /// </summary>
    public KyrolusActiveHealthCheckOptions? Active { get; init; }

    /// <summary>
    /// Gets or sets the passive health check observation options.
    /// </summary>
    public KyrolusPassiveHealthCheckOptions? Passive { get; init; }

    /// <summary>
    /// Gets or sets the policy for determining available destinations (e.g., <c>"HealthyOrUnspecified"</c>).
    /// </summary>
    public string? AvailableDestinationsPolicy { get; init; } = "HealthyOrUnspecified";
}
