namespace KyrolusSous.Caching.Redis;

/// <summary>
/// ASP.NET Core <see cref="IHealthCheck"/> implementation for Redis cache connectivity and PING roundtrip latency.
/// </summary>
/// <remarks>
/// <b>Real-World Use Case:</b>
/// Integrated into <c>/health</c> and <c>/health/ready</c> HTTP endpoints to alert DevOps monitoring 
/// or trigger Kubernetes automated pod restarts if the Redis connection drops.
/// </remarks>
/// <param name="multiplexer">The active Redis connection multiplexer.</param>
/// <param name="options">Health check options.</param>
public sealed class KyrolusRedisCacheHealthCheck(
    IConnectionMultiplexer multiplexer,
    KyrolusRedisCacheHealthCheckOptions? options = null) : IHealthCheck
{
    private readonly IConnectionMultiplexer multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
    private readonly KyrolusRedisCacheHealthCheckOptions options = options ?? new KyrolusRedisCacheHealthCheckOptions();

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!multiplexer.IsConnected)
        {
            return new HealthCheckResult(options.FailureStatus, "Redis is not connected.");
        }

        try
        {
            var db = multiplexer.GetDatabase();
            var timeout = options.Timeout ?? context.Registration.Timeout;
            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(2);
            }
            var latency = await db.PingAsync().WaitAsync(timeout, cancellationToken).ConfigureAwait(false);

            return options.IncludeLatency
                ? HealthCheckResult.Healthy("Redis ping succeeded.", new Dictionary<string, object> { ["latency_ms"] = latency.TotalMilliseconds })
                : HealthCheckResult.Healthy("Redis ping succeeded.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(options.FailureStatus, "Redis ping failed.", ex);
        }
    }
}
