namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisCircuitBreakerTests
{
    [Fact(DisplayName = "KyrolusRedisCircuitBreaker: Should stay closed initially and trip to OPEN after failure threshold reached")]
    public async Task CircuitBreaker_TripsOpen_AfterFailureThreshold()
    {
        var options = new KyrolusRedisCircuitBreakerOptions
        {
            Enabled = true,
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromMilliseconds(100),
            HalfOpenSuccesses = 1
        };

        var cb = new KyrolusRedisCircuitBreaker(options);

        // Initially closed
        cb.TryEnter(out var retryAfter).ShouldBeTrue();
        retryAfter.ShouldBeNull();

        // 2 failures -> still closed
        cb.ReportFailure();
        cb.ReportFailure();
        cb.TryEnter(out _).ShouldBeTrue();

        // 3rd failure -> trips OPEN!
        cb.ReportFailure();
        cb.TryEnter(out var retryAfterOpen).ShouldBeFalse();
        retryAfterOpen.ShouldNotBeNull();
        retryAfterOpen.Value.TotalMilliseconds.ShouldBeGreaterThan(0);

        // Wait for open duration to expire -> Half-Open state
        await Task.Delay(120);
        cb.TryEnter(out _).ShouldBeTrue();

        // Report success in Half-Open state -> Closes circuit
        cb.ReportSuccess();
        cb.TryEnter(out _).ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusRedisCircuitBreaker: When disabled, should always allow entry")]
    public void DisabledCircuitBreaker_AlwaysAllowsEntry()
    {
        var options = new KyrolusRedisCircuitBreakerOptions { Enabled = false };
        var cb = new KyrolusRedisCircuitBreaker(options);

        for (var i = 0; i < 10; i++)
        {
            cb.ReportFailure();
        }

        cb.TryEnter(out var retryAfter).ShouldBeTrue();
        retryAfter.ShouldBeNull();
    }
}
