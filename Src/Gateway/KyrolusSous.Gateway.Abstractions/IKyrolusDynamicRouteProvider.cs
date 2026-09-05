namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Contract for dynamically providing and reloading gateway routes and clusters.
/// </summary>
public interface IKyrolusDynamicRouteProvider
{
    Task<IReadOnlyList<KyrolusGatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusGatewayCluster>> GetClustersAsync(CancellationToken cancellationToken = default);
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
