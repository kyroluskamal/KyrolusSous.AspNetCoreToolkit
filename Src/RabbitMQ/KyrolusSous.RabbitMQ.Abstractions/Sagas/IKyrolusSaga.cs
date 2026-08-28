namespace KyrolusSous.RabbitMQ.Abstractions.Sagas;

/// <summary>
/// State definition for a distributed saga / process manager.
/// </summary>
public interface IKyrolusSagaState
{
    string CorrelationId { get; set; }
    string CurrentState { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    bool IsCompleted { get; set; }
    bool IsFaulted { get; set; }
}

/// <summary>
/// Base class for a distributed saga state instance.
/// </summary>
public class KyrolusSagaState : IKyrolusSagaState
{
    public string CorrelationId { get; set; } = string.Empty;
    public string CurrentState { get; set; } = "Initial";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsCompleted { get; set; }
    public bool IsFaulted { get; set; }
    public Dictionary<string, string> Data { get; set; } = [];
}

/// <summary>
/// Defines a distributed saga process manager with forward steps and compensating actions.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface IKyrolusSaga<TState> where TState : class, IKyrolusSagaState
{
    Task ExecuteStepAsync(
        TState state,
        string stepName,
        Func<Task> action,
        Func<Task>? compensatingAction = null,
        CancellationToken cancellationToken = default);

    Task CompensateAsync(TState state, CancellationToken cancellationToken = default);
}

/// <summary>
/// Storage-agnostic persistence contract for saga states.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface IKyrolusSagaStore<TState> where TState : class, IKyrolusSagaState
{
    Task<TState?> FindAsync(string correlationId, CancellationToken cancellationToken = default);
    Task SaveAsync(TState state, CancellationToken cancellationToken = default);
    Task DeleteAsync(string correlationId, CancellationToken cancellationToken = default);
}
