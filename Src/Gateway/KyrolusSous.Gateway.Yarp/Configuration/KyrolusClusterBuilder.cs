using KyrolusSous.Gateway.Abstractions;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Fluent builder for configuring a cluster, its destinations, and its associated child routes in a single block.
/// Eliminates the need to repeat ClusterId across routes.
/// </summary>
public sealed class KyrolusClusterBuilder
{
    private readonly string _clusterId;
    private string? _loadBalancingPolicy;
    private readonly Dictionary<string, KyrolusGatewayDestination> _destinations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KyrolusGatewayRoute> _routes = [];

    public KyrolusClusterBuilder(string clusterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        _clusterId = clusterId;
    }

    /// <summary>
    /// Sets the load balancing policy for the cluster.
    /// Use constants from <see cref="KyrolusLoadBalancingPolicies"/> (e.g. RoundRobin, LeastRequests, Random, PowerOfTwoChoices) or a custom policy name.
    /// </summary>
    public KyrolusClusterBuilder WithLoadBalancing(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _loadBalancingPolicy = policy;
        return this;
    }

    /// <summary>
    /// Adds a destination endpoint to the cluster.
    /// </summary>
    public KyrolusClusterBuilder AddDestination(string destinationId, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        _destinations[destinationId] = new KyrolusGatewayDestination(address);
        return this;
    }

    /// <summary>
    /// Adds a destination endpoint to the cluster.
    /// </summary>
    public KyrolusClusterBuilder AddDestination(string destinationId, KyrolusGatewayDestination destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        ArgumentNullException.ThrowIfNull(destination);
        _destinations[destinationId] = destination;
        return this;
    }

    /// <summary>
    /// Adds a child route belonging directly to this cluster.
    /// </summary>
    public KyrolusClusterBuilder AddRoute(string routeId, string path, params string[] httpMethods)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _routes.Add(new KyrolusGatewayRoute
        {
            RouteId = routeId,
            ClusterId = _clusterId,
            Match = new KyrolusGatewayRouteMatch
            {
                Path = path,
                Methods = httpMethods.Length > 0 ? httpMethods : null
            }
        });

        return this;
    }

    /// <summary>
    /// Adds a child route with advanced matching criteria belonging directly to this cluster.
    /// </summary>
    public KyrolusClusterBuilder AddRoute(
        string routeId,
        string path,
        IReadOnlyList<string>? methods = null,
        IReadOnlyList<string>? hosts = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _routes.Add(new KyrolusGatewayRoute
        {
            RouteId = routeId,
            ClusterId = _clusterId,
            Match = new KyrolusGatewayRouteMatch
            {
                Path = path,
                Methods = methods,
                Hosts = hosts
            },
            Metadata = metadata
        });

        return this;
    }

    /// <summary>
    /// Builds and returns the configured cluster and all associated routes.
    /// </summary>
    public (KyrolusGatewayCluster Cluster, IReadOnlyList<KyrolusGatewayRoute> Routes) Build()
    {
        var cluster = new KyrolusGatewayCluster
        {
            ClusterId = _clusterId,
            Destinations = _destinations,
            LoadBalancingPolicy = _loadBalancingPolicy
        };

        return (cluster, _routes.AsReadOnly());
    }
}
