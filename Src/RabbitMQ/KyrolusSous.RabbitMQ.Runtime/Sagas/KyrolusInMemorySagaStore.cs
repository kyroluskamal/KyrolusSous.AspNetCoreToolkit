using System.Collections.Concurrent;
using KyrolusSous.RabbitMQ.Abstractions.Sagas;

namespace KyrolusSous.RabbitMQ.Runtime.Sagas;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IKyrolusSagaStore{TState}"/>.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public class KyrolusInMemorySagaStore<TState> : IKyrolusSagaStore<TState> where TState : class, IKyrolusSagaState
{
    private readonly ConcurrentDictionary<string, TState> _states = new();

    public Task<TState?> FindAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        _states.TryGetValue(correlationId, out var state);
        return Task.FromResult(state);
    }

    public Task SaveAsync(TState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.UpdatedAt = DateTimeOffset.UtcNow;
        _states[state.CorrelationId] = state;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        _states.TryRemove(correlationId, out _);
        return Task.CompletedTask;
    }
}
