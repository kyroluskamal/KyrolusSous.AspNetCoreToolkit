using System.Collections.Concurrent;

namespace KyrolusSous.Resilience;

/// <summary>
/// In-memory default implementation of <see cref="IKyrolusCircuitBreakerStateStore"/>.
/// </summary>
public class KyrolusInMemoryCircuitBreakerStateStore : IKyrolusCircuitBreakerStateStore
{
    private readonly ConcurrentDictionary<string, KyrolusCircuitState> _states = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, KyrolusCircuitState>? OnRemoteStateChanged;

    public Task<KyrolusCircuitState> GetStateAsync(string pipelineName, CancellationToken cancellationToken = default)
    {
        var state = _states.TryGetValue(pipelineName, out var s) ? s : KyrolusCircuitState.Closed;
        return Task.FromResult(state);
    }

    public Task SetStateAsync(string pipelineName, KyrolusCircuitState state, CancellationToken cancellationToken = default)
    {
        _states.AddOrUpdate(pipelineName, state, (_, _) => state);
        OnRemoteStateChanged?.Invoke(pipelineName, state);
        return Task.CompletedTask;
    }
}
