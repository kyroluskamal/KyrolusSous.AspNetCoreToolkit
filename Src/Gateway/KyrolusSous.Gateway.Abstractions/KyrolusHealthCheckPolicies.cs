namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Provides strongly-typed constants for active and passive cluster destination health check policies.
/// Eliminates magic strings and ensures compile-time safety.
/// </summary>
public static class KyrolusHealthCheckPolicies
{
    /// <summary>
    /// Active health check policy that marks a destination unhealthy after a consecutive number of probe failures.
    /// </summary>
    public const string ConsecutiveFailures = "ConsecutiveFailures";

    /// <summary>
    /// Passive health check policy that monitors request failures (transport errors, 5xx codes) during client proxying.
    /// </summary>
    public const string TransportFailureRate = "TransportFailureRate";

    /// <summary>
    /// Available destinations policy that considers healthy destinations and destinations with unknown status available for routing.
    /// </summary>
    public const string HealthyAndUnknown = "HealthyAndUnknown";

    /// <summary>
    /// Available destinations policy that considers healthy destinations and unmonitored destinations available for routing.
    /// </summary>
    public const string HealthyOrUnspecified = "HealthyOrUnspecified";
}
