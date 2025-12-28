namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenProjectionManager
{
    Task RebuildAsync(CancellationToken cancellationToken = default);
    Task AssertIsUpToDateAsync(CancellationToken cancellationToken = default);
}
