using System.Threading.RateLimiting;
using KyrolusSous.Resilience;

namespace KyrolusSous.RabbitMQ.Runtime.RateLimiting;

/// <summary>
/// Bridge connecting any standard BCL <see cref="RateLimiter"/> to RabbitMQ message consumers.
/// </summary>
public sealed class KyrolusResilienceConsumerRateLimiter(RateLimiter rateLimiter) : IKyrolusConsumerRateLimiter, IDisposable
{
    private readonly RateLimiter _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

    public async ValueTask AcquireAsync(int permits = 1, CancellationToken cancellationToken = default)
    {
        var lease = await _rateLimiter.AcquireAsync(permits, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException("Failed to acquire rate limiter lease for RabbitMQ consumer.");
        }
    }

    public bool TryAcquire(int permits = 1)
    {
        var lease = _rateLimiter.AttemptAcquire(permits);
        return lease.IsAcquired;
    }

    public void Dispose() => _rateLimiter.Dispose();
}

/// <summary>
/// Partitioned consumer rate limiter isolating limits per queue name, tenant, or partition key using <see cref="IKyrolusPartitionedRateLimiter"/>.
/// </summary>
public sealed class KyrolusPartitionedConsumerRateLimiter(
    IKyrolusPartitionedRateLimiter partitionedLimiter,
    string partitionKey) : IKyrolusConsumerRateLimiter
{
    private readonly IKyrolusPartitionedRateLimiter _partitionedLimiter = partitionedLimiter ?? throw new ArgumentNullException(nameof(partitionedLimiter));
    private readonly string _partitionKey = string.IsNullOrWhiteSpace(partitionKey) ? "default" : partitionKey;

    public async ValueTask AcquireAsync(int permits = 1, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_partitionedLimiter.TryAcquire(_partitionKey))
            {
                return;
            }
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public bool TryAcquire(int permits = 1)
    {
        return _partitionedLimiter.TryAcquire(_partitionKey);
    }
}
