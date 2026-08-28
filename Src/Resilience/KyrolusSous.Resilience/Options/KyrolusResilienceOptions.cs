namespace KyrolusSous.Resilience;

public class KyrolusResilienceOptions
{
    public KyrolusRetryOptionsConfig Retry { get; set; } = new();

    public KyrolusCircuitBreakerOptionsConfig CircuitBreaker { get; set; } = new();

    public KyrolusTimeoutOptionsConfig Timeout { get; set; } = new();

    public KyrolusRateLimiterOptionsConfig RateLimiter { get; set; } = new();

    public KyrolusChaosOptionsConfig Chaos { get; set; } = new();

    public KyrolusAdaptiveThrottlingOptionsConfig AdaptiveThrottling { get; set; } = new();
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
