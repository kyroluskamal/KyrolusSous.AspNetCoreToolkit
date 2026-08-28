namespace KyrolusSous.RabbitMQ.Runtime.RateLimiting;

/// <summary>
/// Abstraction for rate-limiting message consumption.
/// </summary>
public interface IKyrolusConsumerRateLimiter
{
    ValueTask AcquireAsync(int permits = 1, CancellationToken cancellationToken = default);
    bool TryAcquire(int permits = 1);
}

/// <summary>
/// High-efficiency Token Bucket rate limiter for message consumers.
/// </summary>
public class KyrolusTokenBucketRateLimiter : IKyrolusConsumerRateLimiter
{
    private readonly double _capacity;
    private readonly double _refillRatePerSecond;
    private double _availableTokens;
    private DateTimeOffset _lastRefillTime;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public KyrolusTokenBucketRateLimiter(double maxTokensPerSecond, double? burstCapacity = null)
    {
        _refillRatePerSecond = Math.Max(0.1, maxTokensPerSecond);
        _capacity = Math.Max(1.0, burstCapacity ?? maxTokensPerSecond);
        _availableTokens = _capacity;
        _lastRefillTime = DateTimeOffset.UtcNow;
    }

    private void Refill()
    {
        var now = DateTimeOffset.UtcNow;
        var elapsedSeconds = (now - _lastRefillTime).TotalSeconds;
        if (elapsedSeconds > 0)
        {
            _availableTokens = Math.Min(_capacity, _availableTokens + elapsedSeconds * _refillRatePerSecond);
            _lastRefillTime = now;
        }
    }

    public async ValueTask AcquireAsync(int permits = 1, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfIfCancellationRequested(cancellationToken);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Refill();
                if (_availableTokens >= permits)
                {
                    _availableTokens -= permits;
                    return;
                }

                var missingTokens = permits - _availableTokens;
                var waitTimeSeconds = missingTokens / _refillRatePerSecond;
                var delay = TimeSpan.FromSeconds(Math.Max(0.01, waitTimeSeconds));

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    public bool TryAcquire(int permits = 1)
    {
        _lock.Wait();
        try
        {
            Refill();
            if (_availableTokens >= permits)
            {
                _availableTokens -= permits;
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }
}

internal static class CancellationTokenExtensions
{
    public static void ThrowIfIfCancellationRequested(this CancellationToken ct, CancellationToken _)
    {
        ct.ThrowIfCancellationRequested();
    }
}
