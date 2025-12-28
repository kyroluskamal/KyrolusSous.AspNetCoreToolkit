namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

/// <summary>
/// Projection orchestration contract for coordinating rebuilds and live application.
/// </summary>
public interface IKyrolusMartenProjectionOrchestrator
{
    Task EnqueueRebuildAsync(string projectionName, CancellationToken cancellationToken = default);
    Task ApplyEventAsync(object @event, CancellationToken cancellationToken = default);
    Task EnsureUpToDateAsync(string projectionName, CancellationToken cancellationToken = default);
}
