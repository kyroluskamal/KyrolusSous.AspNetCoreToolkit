namespace KyrolusSous.Caching.Redis;

internal sealed class KyrolusRedisCircuitBreaker
{
    private readonly KyrolusRedisCircuitBreakerOptions options;
    private int failureCount;
    private int successCount;
    private int openCount;
    private long openUntilTicks;

    public KyrolusRedisCircuitBreaker(KyrolusRedisCircuitBreakerOptions? options)
    {
        this.options = options ?? new KyrolusRedisCircuitBreakerOptions();
    }

    public bool TryEnter(out TimeSpan? retryAfter)
    {
        retryAfter = null;
        if (!options.Enabled)
        {
            return true;
        }

        var until = Interlocked.Read(ref openUntilTicks);
        if (until <= 0)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow.UtcTicks;
        if (now >= until)
        {
            return true;
        }

        retryAfter = TimeSpan.FromTicks(until - now);
        return false;
    }

    public void ReportSuccess()
    {
        if (!options.Enabled)
        {
            return;
        }

        var until = Interlocked.Read(ref openUntilTicks);
        if (until > 0 && DateTimeOffset.UtcNow.UtcTicks >= until)
        {
            if (Interlocked.Increment(ref successCount) >= Math.Max(1, options.HalfOpenSuccesses))
            {
                Close();
            }
            return;
        }

        Interlocked.Exchange(ref failureCount, 0);
        Interlocked.Exchange(ref successCount, 0);
        Interlocked.Exchange(ref openCount, 0);
        Interlocked.Exchange(ref openUntilTicks, 0);
    }

    public void ReportFailure()
    {
        if (!options.Enabled)
        {
            return;
        }

        if (Interlocked.Increment(ref failureCount) < Math.Max(1, options.FailureThreshold))
        {
            return;
        }

        Open();
    }

    private void Open()
    {
        var count = Interlocked.Increment(ref openCount);
        var duration = ComputeOpenDuration(count);
        Interlocked.Exchange(ref openUntilTicks, DateTimeOffset.UtcNow.Add(duration).UtcTicks);
        Interlocked.Exchange(ref failureCount, 0);
        Interlocked.Exchange(ref successCount, 0);
    }

    private void Close()
    {
        Interlocked.Exchange(ref openUntilTicks, 0);
        Interlocked.Exchange(ref failureCount, 0);
        Interlocked.Exchange(ref successCount, 0);
        Interlocked.Exchange(ref openCount, 0);
    }

    private TimeSpan ComputeOpenDuration(int attempt)
    {
        var baseDuration = options.OpenDuration <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(5)
            : options.OpenDuration;
        var multiplier = Math.Max(1, options.BackoffMultiplier);
        var factor = Math.Pow(multiplier, Math.Max(0, attempt - 1));
        var duration = TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * factor);
        if (options.MaxOpenDuration is { } max && max > TimeSpan.Zero && duration > max)
        {
            return max;
        }

        return duration;
    }
}
