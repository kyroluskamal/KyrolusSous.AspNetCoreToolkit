namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisCircuitOpenException : InvalidOperationException
{
    public KyrolusRedisCircuitOpenException(TimeSpan? retryAfter)
        : base(retryAfter.HasValue
            ? $"Redis circuit is open. Retry after {retryAfter.Value.TotalSeconds:F1}s."
            : "Redis circuit is open.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}
