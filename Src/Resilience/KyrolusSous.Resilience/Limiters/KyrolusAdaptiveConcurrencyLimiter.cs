namespace KyrolusSous.Resilience;

/// <summary>
/// TCP Vegas / Gradient based adaptive concurrency limiter that adjusts limits dynamically to prevent server saturation.
/// </summary>
public class KyrolusAdaptiveConcurrencyLimiter : IKyrolusAdaptiveConcurrencyLimiter
{
    private readonly int _minLimit;
    private readonly int _maxLimit;
    private readonly double _smoothingFactor;

    private int _currentLimit;
    private int _inFlight;
    private double _minRttMs = double.MaxValue;
    private double _emaRttMs = 0;
    private readonly Lock _lock = new();

    public int CurrentLimit => _currentLimit;
    public int InFlightRequests => _inFlight;

    public KyrolusAdaptiveConcurrencyLimiter(int initialLimit = 50, int minLimit = 5, int maxLimit = 1000, double smoothingFactor = 0.2)
    {
        _currentLimit = Math.Clamp(initialLimit, minLimit, maxLimit);
        _minLimit = minLimit;
        _maxLimit = maxLimit;
        _smoothingFactor = Math.Clamp(smoothingFactor, 0.01, 1.0);
    }

    public bool TryAcquire()
    {
        lock (_lock)
        {
            if (_inFlight >= _currentLimit)
            {
                return false;
            }

            _inFlight++;
            return true;
        }
    }

    public void Release(TimeSpan executionDuration, bool success)
    {
        var rttMs = executionDuration.TotalMilliseconds;

        lock (_lock)
        {
            _inFlight = Math.Max(0, _inFlight - 1);

            if (!success)
            {
                // Immediate backoff on failure
                _currentLimit = Math.Max(_minLimit, (int)(_currentLimit * 0.8));
                return;
            }

            if (rttMs < _minRttMs)
            {
                _minRttMs = rttMs;
            }

            if (_emaRttMs <= 0)
            {
                _emaRttMs = rttMs;
            }
            else
            {
                _emaRttMs = (_smoothingFactor * rttMs) + ((1.0 - _smoothingFactor) * _emaRttMs);
            }

            // Gradient calculation (Vegas style: expected vs actual)
            if (_minRttMs > 0 && _emaRttMs > 0)
            {
                var gradient = _minRttMs / _emaRttMs;
                if (gradient >= 0.95)
                {
                    // Low queueing / latency is fast: incrementally grow limit
                    _currentLimit = Math.Min(_maxLimit, _currentLimit + 1);
                }
                else if (gradient < 0.7)
                {
                    // High latency / queueing detected: back off limit
                    _currentLimit = Math.Max(_minLimit, (int)(_currentLimit * gradient));
                }
            }
        }
    }
}
