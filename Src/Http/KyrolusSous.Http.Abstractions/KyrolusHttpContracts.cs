namespace KyrolusSous.Http.Abstractions;

public sealed class KyrolusHttpClientOptions
{
    public string? BaseAddress { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool PropagateAuthToken { get; set; } = true;
    public bool PropagateCorrelationId { get; set; } = true;
    public bool EnableResiliencePipeline { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}

public interface IKyrolusTokenPropagator
{
    ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}

public interface IKyrolusCorrelationPropagator
{
    string? GetCorrelationId();
}
