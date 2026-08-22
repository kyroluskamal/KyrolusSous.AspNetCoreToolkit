namespace KyrolusSous.Resilience;

public class KyrolusResilienceOptions
{
    public RetryOptionsConfig Retry { get; set; } = new();

    public CircuitBreakerOptionsConfig CircuitBreaker { get; set; } = new();

    public TimeoutOptionsConfig Timeout { get; set; } = new();

    public RateLimiterOptionsConfig RateLimiter { get; set; } = new();

    public ChaosOptionsConfig Chaos { get; set; } = new();

    public AdaptiveThrottlingOptionsConfig AdaptiveThrottling { get; set; } = new();
}

public class RetryOptionsConfig
{
    public int MaxRetryAttempts { get; set; } = 3;

    public int InitialDelayMs { get; set; } = 200;

    public double BackoffMultiplier { get; set; } = 2.0;

    public bool UseJitter { get; set; } = true;
}

public class CircuitBreakerOptionsConfig
{
    public double FailureRatio { get; set; } = 0.5;

    public int MinimumThroughput { get; set; } = 10;

    public int SamplingDurationSeconds { get; set; } = 10;

    public int BreakDurationSeconds { get; set; } = 30;
}

public class TimeoutOptionsConfig
{
    public int TotalTimeoutSeconds { get; set; } = 30;

    public int AttemptTimeoutSeconds { get; set; } = 10;
}

public class RateLimiterOptionsConfig
{
    public int PermitLimit { get; set; } = 100;

    public int QueueLimit { get; set; } = 10;
}

public class ChaosOptionsConfig
{
    public bool Enabled { get; set; } = false;

    public double InjectionRate { get; set; } = 0.05;

    public int InjectedLatencyMs { get; set; } = 200;

    public bool InjectTransientErrors { get; set; } = false;
}

public class AdaptiveThrottlingOptionsConfig
{
    public bool Enabled { get; set; } = false;

    public int MaxCpuThresholdPercent { get; set; } = 85;
}
