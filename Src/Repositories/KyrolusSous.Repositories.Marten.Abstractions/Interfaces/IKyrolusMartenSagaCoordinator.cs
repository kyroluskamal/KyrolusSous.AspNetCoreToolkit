namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

/// <summary>
/// Saga coordinator contract for long-running workflows on Marten-backed systems.
/// </summary>
public interface IKyrolusMartenSagaCoordinator
{
    Task<Guid> StartAsync(object sagaState, CancellationToken cancellationToken = default);
    Task<bool> ContinueAsync(Guid sagaId, object message, CancellationToken cancellationToken = default);
    Task<object?> GetStateAsync(Guid sagaId, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid sagaId, CancellationToken cancellationToken = default);
}
