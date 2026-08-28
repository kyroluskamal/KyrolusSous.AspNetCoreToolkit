namespace KyrolusSous.RabbitMQ.Abstractions.Idempotency;

/// <summary>
/// Storage-agnostic abstraction for message idempotency, distributed locking, and result caching.
/// </summary>
public interface IKyrolusIdempotencyStore
{
    Task<bool> TryAcquireLockAsync(string idempotencyKey, TimeSpan lockDuration, CancellationToken cancellationToken = default);
    Task SetResultAsync(string idempotencyKey, string result, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task<string?> GetResultAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Specifies that a message consumer or event handler must be executed idempotently.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusIdempotentAttribute : Attribute
{
    public string? KeyTemplate { get; set; }
    public int LockDurationSeconds { get; set; } = 30;
    public int ResultTtlSeconds { get; set; } = 86400; // 24 hours

    public KyrolusIdempotentAttribute(string? keyTemplate = null)
    {
        KeyTemplate = keyTemplate;
    }
}
