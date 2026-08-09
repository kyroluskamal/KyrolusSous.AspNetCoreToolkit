using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisCacheHealthCheck(
    IConnectionMultiplexer multiplexer,
    KyrolusRedisCacheHealthCheckOptions? options = null) : IHealthCheck
{
    private readonly IConnectionMultiplexer multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
    private readonly KyrolusRedisCacheHealthCheckOptions options = options ?? new KyrolusRedisCacheHealthCheckOptions();

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
