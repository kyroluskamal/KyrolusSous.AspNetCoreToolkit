using System.Collections.Concurrent;

namespace KyrolusSous.Resilience;

/// <summary>
/// Thread-safe implementation of <see cref="IKyrolusCircuitBreakerObserver"/> tracking live circuit states, statistics, manual overrides, and alerts.
/// </summary>
public class KyrolusCircuitBreakerObserver : IKyrolusCircuitBreakerObserver
{
    private sealed class CircuitMetadata
    {
        public KyrolusCircuitState State { get; set; } = KyrolusCircuitState.Closed;
        public DateTimeOffset LastStateChangeUtc { get; set; } = DateTimeOffset.UtcNow;
        public long TotalRequests;
        public long TotalFailures;
    }

    private readonly ConcurrentDictionary<string, CircuitMetadata> _circuits = new(StringComparer.OrdinalIgnoreCase);
    private readonly IKyrolusCircuitBreakerStateStore? _stateStore;
    private readonly IKyrolusResilienceAlertSink? _alertSink;

    public event Action<string, KyrolusCircuitState>? OnCircuitStateChanged;

    public KyrolusCircuitBreakerObserver(
        IKyrolusCircuitBreakerStateStore? stateStore = null,
        IKyrolusResilienceAlertSink? alertSink = null)
    {
        _stateStore = stateStore;
        _alertSink = alertSink;

        if (_stateStore is not null)
        {
            _stateStore.OnRemoteStateChanged += (name, state) =>
            {
                SetLocalCircuitState(name, state, broadcast: false);
            };
        }
    }

    public KyrolusCircuitState GetCircuitState(string pipelineName = "default")
    {
        return _circuits.TryGetValue(pipelineName, out var meta) ? meta.State : KyrolusCircuitState.Closed;
    }

    public KyrolusCircuitBreakerInfo GetCircuitInfo(string pipelineName = "default")
    {
        var meta = _circuits.GetOrAdd(pipelineName, _ => new CircuitMetadata());
        var totalReq = Interlocked.Read(ref meta.TotalRequests);
        var totalFail = Interlocked.Read(ref meta.TotalFailures);
        var ratio = totalReq > 0 ? (double)totalFail / totalReq : 0.0;

        return new KyrolusCircuitBreakerInfo(
            PipelineName: pipelineName,
            State: meta.State,
            LastStateChangeUtc: meta.LastStateChangeUtc,
            TotalRequests: totalReq,
            TotalFailures: totalFail,
            FailureRatio: Math.Round(ratio, 4));
    }

    public IReadOnlyDictionary<string, KyrolusCircuitState> GetAllCircuitStates()
    {
        return _circuits.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.State, StringComparer.OrdinalIgnoreCase);
    }

    public void ForceOpen(string pipelineName)
    {
        SetCircuitState(pipelineName, KyrolusCircuitState.Open);
    }

    public void ForceClose(string pipelineName)
    {
        SetCircuitState(pipelineName, KyrolusCircuitState.Closed);
    }

    public void Reset(string pipelineName)
    {
        if (_circuits.TryGetValue(pipelineName, out var meta))
        {
            Interlocked.Exchange(ref meta.TotalRequests, 0);
            Interlocked.Exchange(ref meta.TotalFailures, 0);
        }
        SetCircuitState(pipelineName, KyrolusCircuitState.Closed);
    }

    public void RecordRequest(string pipelineName, bool success)
    {
        var meta = _circuits.GetOrAdd(pipelineName, _ => new CircuitMetadata());
        Interlocked.Increment(ref meta.TotalRequests);
        if (!success)
        {
            Interlocked.Increment(ref meta.TotalFailures);
        }
    }

    public void SetCircuitState(string pipelineName, KyrolusCircuitState newState)
    {
        SetLocalCircuitState(pipelineName, newState, broadcast: true);
    }

    private void SetLocalCircuitState(string pipelineName, KyrolusCircuitState newState, bool broadcast)
    {
        var stateChanged = false;
        _circuits.AddOrUpdate(
            pipelineName,
            addValueFactory: _ =>
            {
                stateChanged = true;
                return new CircuitMetadata
                {
                    State = newState,
                    LastStateChangeUtc = DateTimeOffset.UtcNow
                };
            },
            updateValueFactory: (_, meta) =>
            {
                if (meta.State != newState)
                {
                    meta.State = newState;
                    meta.LastStateChangeUtc = DateTimeOffset.UtcNow;
                    stateChanged = true;
                }
                return meta;
            });

        if (stateChanged)
        {
            OnCircuitStateChanged?.Invoke(pipelineName, newState);

            if (_alertSink is not null && newState == KyrolusCircuitState.Open)
            {
                _ = _alertSink.PublishAlertAsync(new KyrolusResilienceAlert(
                    pipelineName,
                    newState,
                    $"Circuit breaker tripped to OPEN on pipeline '{pipelineName}'.",
                    DateTimeOffset.UtcNow));
            }

            if (broadcast && _stateStore is not null)
            {
                _ = _stateStore.SetStateAsync(pipelineName, newState);
            }
        }
    }
}
