namespace KyrolusSous.Gateway.Yarp.Configuration;

/// <summary>
/// Fluent scoped builder for configuring a cluster, its backend destination endpoints, and its associated child routes in a single block.
/// Eliminates the need to repeat <c>ClusterId</c> across routes and ensures structural cohesion.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why use KyrolusClusterBuilder?</b><br/>
/// In standard YARP configurations, routes and clusters are defined separately, forcing you to type the cluster name
/// repeatedly in every single route. <see cref="KyrolusClusterBuilder"/> inverts this paradigm:
/// you declare the cluster once, add its backend server replicas (<see cref="AddDestination(string, string)"/>),
/// and declare all the child routes belonging to that microservice directly within the same scoped lambda.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// gateway.AddCluster("orders-service", cluster =>
/// {
///     // 1. Choose load balancing:
///     cluster.WithLoadBalancing(KyrolusLoadBalancingPolicies.RoundRobin)
///     
///     // 2. Add backend replicas (Destinations):
///            .AddDestination("node1", "http://10.0.1.10:5000")
///            .AddDestination("node2", "http://10.0.1.11:5000")
///            
///     // 3. Declare routes directly inside the cluster:
///            .AddRoute("orders-get-all", "/api/orders", KyrolusGatewayHttpMethods.Get)
///            .AddRoute("orders-create", "/api/orders", KyrolusGatewayHttpMethods.Post);
/// });
/// </code>
/// </example>
public sealed class KyrolusClusterBuilder
{
    private readonly string _clusterId;
    private string? _loadBalancingPolicy;
    private readonly Dictionary<string, KyrolusGatewayDestination> _destinations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KyrolusGatewayRoute> _routes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusClusterBuilder"/> class for the specified cluster identifier.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the cluster (e.g. <c>"orders-cluster"</c>).</param>
    public KyrolusClusterBuilder(string clusterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        _clusterId = clusterId;
    }

    /// <summary>
    /// Sets the load balancing policy algorithm for distributing traffic among the cluster destinations.
    /// </summary>
    /// <param name="policy">
    /// The policy name. Use strongly-typed constants from <see cref="KyrolusLoadBalancingPolicies"/> (e.g. <c>RoundRobin</c>, <c>LeastRequests</c>, <c>Random</c>, <c>PowerOfTwoChoices</c>) or a custom registered policy name.
    /// </param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithLoadBalancing(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _loadBalancingPolicy = policy;
        return this;
    }

    /// <summary>
    /// Adds a physical backend destination endpoint (replica) to the cluster using an address URI string.
    /// </summary>
    /// <param name="destinationId">A unique identifier for this destination node (e.g., <c>"primary"</c>, <c>"node1"</c>).</param>
    /// <param name="address">The absolute base URI of the target backend service replica (e.g., <c>"http://10.0.1.10:5000"</c>).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <b>Important:</b> This is the address of the internal service instance running the application, NOT the public client URL or endpoint path.
    /// </remarks>
    public KyrolusClusterBuilder AddDestination(string destinationId, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        _destinations[destinationId] = new KyrolusGatewayDestination(address);
        return this;
    }

    /// <summary>
    /// Adds a physical backend destination endpoint to the cluster using a <see cref="KyrolusGatewayDestination"/> instance.
    /// </summary>
    /// <param name="destinationId">A unique identifier for this destination node (e.g., <c>"primary"</c>, <c>"node1"</c>).</param>
    /// <param name="destination">The destination instance containing the base URI.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder AddDestination(string destinationId, KyrolusGatewayDestination destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        ArgumentNullException.ThrowIfNull(destination);
        _destinations[destinationId] = destination;
        return this;
    }

    /// <summary>
    /// Adds a child route belonging directly to this cluster, automatically assigning the cluster identifier.
    /// </summary>
    /// <param name="routeId">The unique route identifier (e.g. <c>"get-orders-route"</c>).</param>
    /// <param name="path">The URL path pattern to match on incoming requests (e.g. <c>"/api/orders/{**catch-all}"</c>).</param>
    /// <param name="httpMethods">Optional list of allowed HTTP methods (use constants from <see cref="KyrolusGatewayHttpMethods"/>). If omitted, matches all verbs.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
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
    /// Adds a child route with advanced matching criteria (hosts, multiple methods, custom metadata) belonging directly to this cluster.
    /// </summary>
    /// <param name="routeId">The unique route identifier.</param>
    /// <param name="path">The URL path pattern to match on incoming requests.</param>
    /// <param name="methods">Optional list of allowed HTTP methods (e.g. GET, POST).</param>
    /// <param name="hosts">Optional list of incoming client hostnames/domains to match (e.g., <c>"api.example.com"</c>).</param>
    /// <param name="metadata">Optional dictionary of custom metadata for transforms and filters.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
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
    /// Builds and returns the configured <see cref="KyrolusGatewayCluster"/> alongside all associated <see cref="KyrolusGatewayRoute"/> instances.
    /// </summary>
    /// <returns>A tuple containing the immutable cluster and its associated child routes.</returns>
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
