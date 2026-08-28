using KyrolusSous.RabbitMQ.Abstractions.Sagas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyrolusSous.RabbitMQ.Runtime.Sagas;

/// <summary>
/// Execution coordinator for distributed saga workflows, managing step transitions and automatic compensations.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public class KyrolusSagaCoordinator<TState> : IKyrolusSaga<TState> where TState : class, IKyrolusSagaState
{
    private readonly IKyrolusSagaStore<TState> _sagaStore;
    private readonly ILogger<KyrolusSagaCoordinator<TState>> _logger;
    private readonly List<Func<Task>> _compensations = [];

    public KyrolusSagaCoordinator(
        IKyrolusSagaStore<TState> sagaStore,
        ILogger<KyrolusSagaCoordinator<TState>>? logger = null)
    {
        _sagaStore = sagaStore ?? throw new ArgumentNullException(nameof(sagaStore));
        _logger = logger ?? NullLogger<KyrolusSagaCoordinator<TState>>.Instance;
    }

    public async Task ExecuteStepAsync(
        TState state,
        string stepName,
        Func<Task> action,
        Func<Task>? compensatingAction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        ArgumentNullException.ThrowIfNull(action);

        _logger.LogInformation("Executing Saga step '{StepName}' for correlation ID {CorrelationId}", stepName, state.CorrelationId);

        try
        {
            state.CurrentState = stepName;
            await action().ConfigureAwait(false);

            if (compensatingAction != null)
            {
                _compensations.Insert(0, compensatingAction); // Push to LIFO stack
            }

            await _sagaStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saga step '{StepName}' failed for correlation ID {CorrelationId}. Initiating compensation...", stepName, state.CorrelationId);
            state.IsFaulted = true;
            state.CurrentState = $"{stepName}.Faulted";
            await _sagaStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);

            await CompensateAsync(state, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task CompensateAsync(TState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        _logger.LogWarning("Running {Count} compensating action(s) for Saga correlation ID {CorrelationId}", _compensations.Count, state.CorrelationId);

        foreach (var compensation in _compensations)
        {
            try
            {
                await compensation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Compensating action failed for Saga correlation ID {CorrelationId}", state.CorrelationId);
            }
        }

        _compensations.Clear();
        state.CurrentState = "Compensated";
        await _sagaStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
    }
}
