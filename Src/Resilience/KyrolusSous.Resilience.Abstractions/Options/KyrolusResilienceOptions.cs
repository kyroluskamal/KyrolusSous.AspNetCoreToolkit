namespace KyrolusSous.Resilience;

/// <summary>
/// Root resilience options containing configuration for retry, circuit breaker, timeout, rate limiter, hedging, chaos, adaptive throttling, and partitioned limiting.
/// </summary>
public class KyrolusResilienceOptions
{
    public KyrolusRetryOptionsConfig Retry { get; set; } = new();

    public KyrolusCircuitBreakerOptionsConfig CircuitBreaker { get; set; } = new();

    public KyrolusTimeoutOptionsConfig Timeout { get; set; } = new();

    public KyrolusRateLimiterOptionsConfig RateLimiter { get; set; } = new();

    public KyrolusHedgingOptionsConfig Hedging { get; set; } = new();

    public KyrolusChaosOptionsConfig Chaos { get; set; } = new();

    public KyrolusAdaptiveThrottlingOptionsConfig AdaptiveThrottling { get; set; } = new();

    public KyrolusPartitionedRateLimiterOptionsConfig PartitionedRateLimiter { get; set; } = new();
}

public class KyrolusRetryOptionsConfig
{
    public int MaxRetryAttempts { get; set; } = 3;

    public int InitialDelayMs { get; set; } = 200;

    public double BackoffMultiplier { get; set; } = 2.0;

    public bool UseJitter { get; set; } = true;
}

public class KyrolusCircuitBreakerOptionsConfig
{
    public double FailureRatio { get; set; } = 0.5;

    public int MinimumThroughput { get; set; } = 10;

    public int SamplingDurationSeconds { get; set; } = 10;

    public int BreakDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Whether staggered warm-up trial is enabled when transitioning to HalfOpen state.
    /// </summary>
    public bool EnableStaggeredWarmUp { get; set; } = false;

    /// <summary>
    /// Percentage of requests (0.01 - 1.0) allowed through during HalfOpen warmup phase.
    /// </summary>
    public double HalfOpenPermitRatio { get; set; } = 0.2;
}

public class KyrolusTimeoutOptionsConfig
{
    public int TotalTimeoutSeconds { get; set; } = 30;

    public int AttemptTimeoutSeconds { get; set; } = 10;
}

public class KyrolusRateLimiterOptionsConfig
{
    public int PermitLimit { get; set; } = 100;

    public int QueueLimit { get; set; } = 10;
}

public class KyrolusHedgingOptionsConfig
{
    /// <summary>
    /// Whether hedging (speculative execution of parallel backup attempts) is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum number of parallel attempts. Default is 2.
    /// </summary>
    public int MaxHedgedAttempts { get; set; } = 2;

    /// <summary>
    /// Delay before initiating a speculative backup execution. Default is 500ms.
    /// </summary>
    public int DelayMs { get; set; } = 500;
}

public class KyrolusChaosOptionsConfig
{
    public bool Enabled { get; set; } = false;

    public double InjectionRate { get; set; } = 0.05;

    public int InjectedLatencyMs { get; set; } = 200;

    public bool InjectTransientErrors { get; set; } = false;
}

public class KyrolusAdaptiveThrottlingOptionsConfig
{
    public bool Enabled { get; set; } = false;

    public int MaxCpuThresholdPercent { get; set; } = 85;
}

public class KyrolusPartitionedRateLimiterOptionsConfig
{
    public bool Enabled { get; set; } = false;

    public int PermitsPerPartition { get; set; } = 20;
}
