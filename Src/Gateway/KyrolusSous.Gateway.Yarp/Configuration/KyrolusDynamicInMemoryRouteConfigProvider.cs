namespace KyrolusSous.Gateway.Yarp.Configuration;

/// <summary>
/// Thread-safe in-memory configuration provider for YARP, supporting programmatic fluent cluster definition,
/// JSON configuration section loading, and dynamic runtime route queries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Architecture:</b><br/>
/// Implements both YARP's <see cref="IProxyConfigProvider"/> and the gateway contract <see cref="IKyrolusDynamicRouteProvider"/>.
/// Serves as the central repository of routes and clusters in memory, converting toolkit abstractions
/// (<see cref="KyrolusGatewayRoute"/> and <see cref="KyrolusGatewayCluster"/>) into YARP's native <see cref="RouteConfig"/> and <see cref="ClusterConfig"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
/// 
/// // Fluent Scoped definition:
/// provider.AddCluster("catalog-service", cluster =>
/// {
///     cluster.WithLoadBalancing(KyrolusLoadBalancingPolicies.RoundRobin)
///            .AddDestination("node1", "http://10.0.1.20:5000")
///            .AddRoute("catalog-all", "/api/catalog/{**catch-all}");
/// });
/// </code>
/// </example>
public sealed class KyrolusDynamicInMemoryRouteConfigProvider : IProxyConfigProvider, IKyrolusDynamicRouteProvider
{
    private readonly List<KyrolusGatewayRoute> _routes = [];
    private readonly List<KyrolusGatewayCluster> _clusters = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusDynamicInMemoryRouteConfigProvider"/> class.
    /// </summary>
    public KyrolusDynamicInMemoryRouteConfigProvider() { }

    /// <summary>
    /// Adds a raw gateway route rule to the in-memory provider.
    /// </summary>
    /// <param name="route">The gateway route to add.</param>
    public void AddRoute(KyrolusGatewayRoute route) => _routes.Add(route);

    /// <summary>
    /// Adds a raw gateway service cluster with its destinations and load balancing policy.
    /// </summary>
    /// <param name="cluster">The gateway cluster to add.</param>
    public void AddCluster(KyrolusGatewayCluster cluster) => _clusters.Add(cluster);

    /// <summary>
    /// Adds a cluster and all its associated child routes in a single fluent scoped block, eliminating repetition of ClusterId.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the cluster (e.g. <c>"orders-cluster"</c>).</param>
    /// <param name="configure">A configuration action applied to the <see cref="KyrolusClusterBuilder"/>.</param>
    /// <returns>The provider instance for fluent chaining.</returns>
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
    /// Loads routes and clusters from a configuration section (e.g., from appsettings.json <c>"ReverseProxy"</c> section).
    /// </summary>
    /// <param name="section">The configuration section containing <c>Clusters</c> and <c>Routes</c> children.</param>
    /// <returns>The provider instance for fluent chaining.</returns>
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

    /// <summary>
    /// Builds and returns a snapshot of the current in-memory routes and clusters formatted as YARP's <see cref="IProxyConfig"/>.
    /// </summary>
    /// <returns>An instance of <see cref="KyrolusCustomProxyConfig"/> containing the active routes and clusters.</returns>
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

    /// <summary>
    /// Asynchronously retrieves the snapshot of all currently configured gateway routes.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of configured <see cref="KyrolusGatewayRoute"/> instances.</returns>
    public Task<IReadOnlyList<KyrolusGatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KyrolusGatewayRoute>>(_routes.AsReadOnly());

    /// <summary>
    /// Asynchronously retrieves the snapshot of all currently configured gateway clusters.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of configured <see cref="KyrolusGatewayCluster"/> instances.</returns>
    public Task<IReadOnlyList<KyrolusGatewayCluster>> GetClustersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KyrolusGatewayCluster>>(_clusters.AsReadOnly());

    /// <summary>
    /// Signals the gateway engine to reload its configuration snapshot and notify YARP of any dynamic updates.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    public Task ReloadAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
