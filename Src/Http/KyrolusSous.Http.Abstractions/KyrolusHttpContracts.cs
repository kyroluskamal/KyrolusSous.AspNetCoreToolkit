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

public sealed class KyrolusHmacOptions
{
    public required string SecretKey { get; set; }
    public string HeaderName { get; set; } = "X-Kyrolus-Signature";
    public string TimestampHeaderName { get; set; } = "X-Kyrolus-Timestamp";
    public TimeSpan MaxAllowedClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}

public interface IKyrolusHmacSigner
{
    string ComputeSignature(string secretKey, string timestamp, string httpMethod, string pathAndQuery, byte[]? body);
    bool VerifySignature(string secretKey, string signature, string timestamp, string httpMethod, string pathAndQuery, byte[]? body);
}

public interface IKyrolusTokenPropagator
{
    ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default);
}

public interface IKyrolusCorrelationPropagator
{
    string? GetCorrelationId();
}
