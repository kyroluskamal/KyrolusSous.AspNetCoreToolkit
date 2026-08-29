namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusPaymentIdempotencyStore
{
    Task<KyrolusPaymentResult?> GetResultAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task SaveResultAsync(string idempotencyKey, KyrolusPaymentResult result, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLockAsync(string idempotencyKey, TimeSpan lockDuration, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}
