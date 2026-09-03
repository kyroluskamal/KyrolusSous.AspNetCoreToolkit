namespace KyrolusSous.CQRS.Caching;

/// <summary>
/// Thrown when a command's idempotency key is already claimed by another in-flight execution.
/// </summary>
/// <remarks>
/// This means a concurrent request with the same <c>IdempotencyKey</c> is still running (its handler
/// has not finished long enough to have written a completed result yet). The safe response is to ask
/// the caller to retry shortly - never to execute the command a second time, which is exactly what
/// idempotency exists to prevent.
/// </remarks>
public sealed class KyrolusIdempotencyConflictException(string idempotencyKey)
    : Exception($"[Kyrolus CQRS] A request with idempotency key '{idempotencyKey}' is already in progress. Retry shortly.")
{
    /// <summary>The idempotency key that is currently claimed by another in-flight execution.</summary>
    public string IdempotencyKey { get; } = idempotencyKey;
}
