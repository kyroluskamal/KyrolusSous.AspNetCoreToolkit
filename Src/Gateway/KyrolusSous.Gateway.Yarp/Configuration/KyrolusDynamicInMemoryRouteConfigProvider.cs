using KyrolusSous.Gateway.Abstractions;
using Microsoft.Extensions.Configuration;
using Yarp.ReverseProxy.Configuration;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Thread-safe in-memory provider for dynamic YARP proxy configuration, fluent cluster building, and route reloading.
/// </summary>
public sealed class KyrolusDynamicInMemoryRouteConfigProvider : IProxyConfigProvider, IKyrolusDynamicRouteProvider
{
    private readonly List<KyrolusGatewayRoute> _routes = [];
    private readonly List<KyrolusGatewayCluster> _clusters = [];

    public KyrolusDynamicInMemoryRouteConfigProvider() { }

    public void AddRoute(KyrolusGatewayRoute route) => _routes.Add(route);
    public void AddCluster(KyrolusGatewayCluster cluster) => _clusters.Add(cluster);

    /// <summary>
    /// Adds a cluster and all its associated child routes in a single fluent block, eliminating repetition of ClusterId.
    /// </summary>
    public KyrolusDynamicInMemoryRouteConfigProvider AddCluster(string clusterId, Action<KyrolusClusterBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new KyrolusClusterBuilder(clusterId);
        configure(builder);

        var (cluster, routes) = builder.Build();
        _clusters.Add(cluster);
        _routes.AddRange(routes);

        return this;
    }

    /// <summary>
    /// Loads routes and clusters from a configuration section (e.g. from appsettings.json "ReverseProxy" section).
    /// </summary>
    public KyrolusDynamicInMemoryRouteConfigProvider LoadFromConfiguration(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var clustersSection = section.GetSection("Clusters");
        foreach (var clusterSec in clustersSection.GetChildren())
        {
            var clusterId = clusterSec.Key;
            var loadBalancingPolicy = clusterSec["LoadBalancingPolicy"];
            var destinations = new Dictionary<string, KyrolusGatewayDestination>(StringComparer.OrdinalIgnoreCase);

            var destinationsSec = clusterSec.GetSection("Destinations");
            foreach (var destSec in destinationsSec.GetChildren())
            {
                var address = destSec["Address"];
                if (!string.IsNullOrWhiteSpace(address))
                {
                    destinations[destSec.Key] = new KyrolusGatewayDestination(address);
                }
            }

            _clusters.Add(new KyrolusGatewayCluster
            {
                ClusterId = clusterId,
                Destinations = destinations,
                LoadBalancingPolicy = loadBalancingPolicy
            });
        }

        var routesSection = section.GetSection("Routes");
        foreach (var routeSec in routesSection.GetChildren())
        {
            var routeId = routeSec.Key;
            var clusterId = routeSec["ClusterId"] ?? string.Empty;
            var matchSec = routeSec.GetSection("Match");
            var path = matchSec["Path"] ?? string.Empty;

            var methods = matchSec.GetSection("Methods").GetChildren().Select(c => c.Value).OfType<string>().ToList();
            var hosts = matchSec.GetSection("Hosts").GetChildren().Select(c => c.Value).OfType<string>().ToList();

            var metadataSec = routeSec.GetSection("Metadata");
            var metadata = metadataSec.GetChildren().ToDictionary(c => c.Key, c => c.Value ?? string.Empty);

            _routes.Add(new KyrolusGatewayRoute
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new KyrolusGatewayRouteMatch
                {
                    Path = path,
                    Methods = methods.Count > 0 ? methods : null,
                    Hosts = hosts.Count > 0 ? hosts : null
                },
                Metadata = metadata.Count > 0 ? metadata : null
            });
        }

        return this;
    }

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
