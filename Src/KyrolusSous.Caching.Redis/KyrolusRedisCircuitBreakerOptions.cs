namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisCircuitBreakerOptions
{
    public bool Enabled { get; set; } = true;
    public int FailureThreshold { get; set; } = 5;
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan? MaxOpenDuration { get; set; } = TimeSpan.FromMinutes(2);
    public double BackoffMultiplier { get; set; } = 2;
    public int HalfOpenSuccesses { get; set; } = 1;
    public bool ThrowOnOpen { get; set; }
}
