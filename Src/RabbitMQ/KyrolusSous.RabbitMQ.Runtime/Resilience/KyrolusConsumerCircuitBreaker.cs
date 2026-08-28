namespace KyrolusSous.RabbitMQ.Runtime.Resilience;

public enum KyrolusCircuitState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Circuit breaker and backpressure controller for RabbitMQ message consumers with Half-Open probe protection.
/// </summary>
public class KyrolusConsumerCircuitBreaker
{
    private readonly int _consecutiveFailureThreshold;
    private readonly TimeSpan _breakDuration;
    private int _failureCount;
    private DateTimeOffset _lastFailureTime;
    private DateTimeOffset _circuitOpenedTime;
    private KyrolusCircuitState _state = KyrolusCircuitState.Closed;
    private bool _halfOpenProbeInFlight;
    private readonly object _lock = new();

    public KyrolusCircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == KyrolusCircuitState.Open && DateTimeOffset.UtcNow >= _circuitOpenedTime + _breakDuration)
                {
                    _state = KyrolusCircuitState.HalfOpen;
                    _halfOpenProbeInFlight = false;
                }

                return _state;
            }
        }
    }

    public KyrolusConsumerCircuitBreaker(int consecutiveFailureThreshold = 5, TimeSpan? breakDuration = null)
    {
        _consecutiveFailureThreshold = Math.Max(1, consecutiveFailureThreshold);
        _breakDuration = breakDuration ?? TimeSpan.FromSeconds(30);
    }

    public bool CanExecute()
    {
        lock (_lock)
        {
            if (_state == KyrolusCircuitState.Open && DateTimeOffset.UtcNow >= _circuitOpenedTime + _breakDuration)
            {
                _state = KyrolusCircuitState.HalfOpen;
                _halfOpenProbeInFlight = false;
            }

            if (_state == KyrolusCircuitState.Closed)
            {
                return true;
            }

            if (_state == KyrolusCircuitState.HalfOpen)
            {
                // Only allow 1 probe execution in flight
                if (!_halfOpenProbeInFlight)
                {
                    _halfOpenProbeInFlight = true;
                    return true;
                }

                return false;
            }

            return false;
        }
    }

    public void ReportSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _halfOpenProbeInFlight = false;
            _state = KyrolusCircuitState.Closed;
        }
    }

    public void ReportFailure()
    {
        lock (_lock)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;
            _failureCount++;
            _halfOpenProbeInFlight = false;

            if (_state == KyrolusCircuitState.HalfOpen || _failureCount >= _consecutiveFailureThreshold)
            {
                _state = KyrolusCircuitState.Open;
                _circuitOpenedTime = DateTimeOffset.UtcNow;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _halfOpenProbeInFlight = false;
            _state = KyrolusCircuitState.Closed;
        }
    }
}
