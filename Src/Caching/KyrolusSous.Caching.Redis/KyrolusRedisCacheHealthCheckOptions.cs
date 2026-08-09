using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisCacheHealthCheckOptions
{
    public HealthStatus FailureStatus { get; set; } = HealthStatus.Unhealthy;
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(2);
    public bool IncludeLatency { get; set; } = true;
}
