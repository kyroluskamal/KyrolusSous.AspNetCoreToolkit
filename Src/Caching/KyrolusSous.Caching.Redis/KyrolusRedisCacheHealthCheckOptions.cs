namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Configures options for the Redis Cache health check probe.
/// </summary>
/// <remarks>
/// <b>Real-World Use Case (Kubernetes Liveness &amp; Readiness Probes):</b>
/// Used by container orchestrators (Kubernetes / Docker Swarm / AWS ECS) to monitor Redis connectivity.
/// If Redis fails to respond within <see cref="Timeout"/> (2s), the probe reports <see cref="FailureStatus"/>, 
/// preventing the load balancer from sending traffic to degraded containers.
/// </remarks>
public sealed class KyrolusRedisCacheHealthCheckOptions
{
    /// <summary>
    /// Gets or sets the health check status reported when Redis is unreachable. Defaults to <see cref="HealthStatus.Unhealthy"/>.
    /// </summary>
    public HealthStatus FailureStatus { get; set; } = HealthStatus.Unhealthy;

    /// <summary>
    /// Gets or sets the timeout duration for the PING command sent to Redis during health checks. Defaults to 2 seconds.
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets whether to include roundtrip PING latency measurements in the health check report data. Defaults to <c>true</c>.
    /// </summary>
    public bool IncludeLatency { get; set; } = true;
}
