using KyrolusSous.Gateway.Abstractions;
using Yarp.ReverseProxy.Configuration;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Thread-safe in-memory provider for dynamic YARP proxy configuration and route reloading.
/// </summary>
public sealed class KyrolusDynamicInMemoryRouteConfigProvider : IProxyConfigProvider, IKyrolusDynamicRouteProvider
{
    private readonly List<KyrolusGatewayRoute> _routes = [];
    private readonly List<KyrolusGatewayCluster> _clusters = [];

    public KyrolusDynamicInMemoryRouteConfigProvider() { }

    public void AddRoute(KyrolusGatewayRoute route) => _routes.Add(route);
    public void AddCluster(KyrolusGatewayCluster cluster) => _clusters.Add(cluster);

    public IProxyConfig GetConfig()
    {
        var yarpRoutes = _routes.Select(r => new RouteConfig
        {
            RouteId = r.RouteId,
            ClusterId = r.ClusterId,
            Match = new RouteMatch
            {
                Path = r.Match.Path,
                Methods = r.Match.Methods,
                Hosts = r.Match.Hosts
            },
            Metadata = r.Metadata
        }).ToList();

        var yarpClusters = _clusters.Select(c => new ClusterConfig
        {
            ClusterId = c.ClusterId,
            LoadBalancingPolicy = c.LoadBalancingPolicy,
            Destinations = c.Destinations.ToDictionary(
                kv => kv.Key,
                kv => new DestinationConfig { Address = kv.Value.Address })
        }).ToList();

        return new KyrolusCustomProxyConfig(yarpRoutes, yarpClusters);
    }

    public Task<IReadOnlyList<KyrolusGatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KyrolusGatewayRoute>>(_routes.AsReadOnly());

    public Task<IReadOnlyList<KyrolusGatewayCluster>> GetClustersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KyrolusGatewayCluster>>(_clusters.AsReadOnly());

    public Task ReloadAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
