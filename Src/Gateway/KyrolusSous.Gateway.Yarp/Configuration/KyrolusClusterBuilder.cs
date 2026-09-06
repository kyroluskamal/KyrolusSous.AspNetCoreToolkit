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
    private KyrolusLoadBalancingPolicy? _loadBalancingPolicy;
    private KyrolusHealthCheckOptions? _healthCheck;
    private KyrolusSessionAffinityOptions? _sessionAffinity;
    private TimeSpan? _httpRequestTimeout;
    private KyrolusHttpClientOptions? _httpClient;
    private bool? _allowResponseBuffering;
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
    /// The policy algorithm. Use standard policies from <see cref="KyrolusLoadBalancingPolicy"/> (e.g. <c>RoundRobin</c>, <c>LeastRequests</c>, <c>Random</c>, <c>PowerOfTwoChoices</c>) or a custom registered policy name.
    /// </param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithLoadBalancing(KyrolusLoadBalancingPolicy policy)
    {
        _loadBalancingPolicy = policy;
        return this;
    }

    /// <summary>
    /// Sets the load balancing policy algorithm for distributing traffic among the cluster destinations by raw policy name.
    /// </summary>
    /// <param name="policy">The policy name or custom registered policy name.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithLoadBalancing(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _loadBalancingPolicy = KyrolusLoadBalancingPolicy.From(policy);
        return this;
    }

    /// <summary>
    /// Configures health check monitoring policies (active probes and passive observation) for cluster destinations.
    /// </summary>
    public KyrolusClusterBuilder WithHealthCheck(KyrolusHealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _healthCheck = options;
        return this;
    }

    /// <summary>
    /// Configures active health check probing for cluster destinations.
    /// The gateway sends periodic HTTP GET requests to the specified endpoint to ensure destination instances are healthy.
    /// </summary>
    /// <param name="path">The HTTP path to query (e.g. <c>"/healthz"</c> or <c>"/api/health"</c>).</param>
    /// <param name="interval">The probing interval. Defaults to 10 seconds.</param>
    /// <param name="timeout">The probe timeout. Defaults to 5 seconds.</param>
    /// <param name="policy">The health check policy algorithm (use <see cref="KyrolusActiveHealthCheckPolicy.ConsecutiveFailures"/> or custom).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithActiveHealthCheck(
        string path,
        TimeSpan? interval = null,
        TimeSpan? timeout = null,
        KyrolusActiveHealthCheckPolicy? policy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _healthCheck = (_healthCheck ?? new KyrolusHealthCheckOptions()) with
        {
            Active = new KyrolusActiveHealthCheckOptions
            {
                Enabled = true,
                Path = path,
                Interval = interval ?? TimeSpan.FromSeconds(10),
                Timeout = timeout ?? TimeSpan.FromSeconds(5),
                Policy = policy ?? KyrolusActiveHealthCheckPolicy.ConsecutiveFailures
            }
        };
        return this;
    }

    /// <summary>
    /// Configures passive health check observation for cluster destinations.
    /// The gateway monitors real proxied requests and temporarily marks destinations unhealthy if they return 5xx errors or connection failures.
    /// </summary>
    /// <param name="reactivationPeriod">The duration before an unhealthy destination is restored to traffic rotation. Defaults to 30 seconds.</param>
    /// <param name="policy">The passive policy algorithm (use <see cref="KyrolusPassiveHealthCheckPolicy.TransportFailureRate"/> or custom).</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithPassiveHealthCheck(
        TimeSpan? reactivationPeriod = null,
        KyrolusPassiveHealthCheckPolicy? policy = null)
    {
        _healthCheck = (_healthCheck ?? new KyrolusHealthCheckOptions()) with
        {
            Passive = new KyrolusPassiveHealthCheckOptions
            {
                Enabled = true,
                ReactivationPeriod = reactivationPeriod ?? TimeSpan.FromSeconds(30),
                Policy = policy ?? KyrolusPassiveHealthCheckPolicy.TransportFailureRate
            }
        };
        return this;
    }

    /// <summary>
    /// Configures session affinity (sticky sessions) for cluster destinations.
    /// </summary>
    public KyrolusClusterBuilder WithSessionAffinity(KyrolusSessionAffinityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _sessionAffinity = options;
        return this;
    }

    /// <summary>
    /// Configures session affinity (sticky sessions) for cluster destinations with custom policy, failure policy, and hardened cookie options.
    /// </summary>
    /// <param name="policy">The affinity mechanism policy name (e.g., <c>"Cookie"</c> or <c>"HashCookie"</c>). Defaults to <c>"Cookie"</c>.</param>
    /// <param name="failurePolicy">The strategy when an affinitized destination is down (<c>"Redistribute"</c> or <c>"Return503Error"</c>). Defaults to <c>"Redistribute"</c>.</param>
    /// <param name="keyName">The name of the cookie or header used for affinity tokens.</param>
    /// <param name="configureCookie">Optional configuration delegate for session affinity cookie security settings.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithSessionAffinity(
        string policy = "Cookie",
        string failurePolicy = "Redistribute",
        string? keyName = null,
        Action<KyrolusSessionAffinityCookieOptions>? configureCookie = null)
    {
        var cookieOptions = new KyrolusSessionAffinityCookieOptions();
        configureCookie?.Invoke(cookieOptions);

        _sessionAffinity = new KyrolusSessionAffinityOptions
        {
            Enabled = true,
            Policy = policy,
            FailurePolicy = failurePolicy,
            AffinityKeyName = keyName ?? ".KyrolusGateway.Affinity",
            Cookie = cookieOptions
        };

        return this;
    }

    /// <summary>
    /// Sets the HTTP request / activity timeout duration for outbound calls to backend destinations in this cluster.
    /// </summary>
    public KyrolusClusterBuilder WithTimeout(TimeSpan timeout)
    {
        _httpRequestTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Configures the outbound HTTP client settings (e.g. SSL bypass, connection limits) for communication with backend destinations.
    /// </summary>
    public KyrolusClusterBuilder WithHttpClient(KyrolusHttpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = options;
        return this;
    }

    /// <summary>
    /// Sets the default HTTP protocol version and version policy for outbound requests to this cluster.
    /// Essential for gRPC services (HTTP/2 with <see cref="HttpVersionPolicy.RequestVersionExact"/>) and HTTP/3.
    /// </summary>
    /// <param name="version">The HTTP version (e.g. <c>HttpVersion.Version20</c> or <c>HttpVersion.Version30</c>).</param>
    /// <param name="policy">The HTTP version policy (e.g. <see cref="HttpVersionPolicy.RequestVersionExact"/> or <see cref="HttpVersionPolicy.RequestVersionOrHigher"/>). Defaults to <see cref="HttpVersionPolicy.RequestVersionOrHigher"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithHttpVersion(Version version, HttpVersionPolicy policy = HttpVersionPolicy.RequestVersionOrHigher)
    {
        ArgumentNullException.ThrowIfNull(version);
        _httpClient = (_httpClient ?? new KyrolusHttpClientOptions()) with
        {
            DefaultVersion = version,
            VersionPolicy = policy
        };
        return this;
    }

    /// <summary>
    /// Configures whether to bypass SSL/TLS server certificate validation when connecting to backend destinations in this cluster.
    /// <para>
    /// <b>Security Warning:</b> Enable ONLY in local development, testing, or internal isolated Docker environments with self-signed certificates.
    /// Never enable in production.
    /// </para>
    /// </summary>
    /// <param name="accept">Whether to accept any server certificate. Defaults to <c>true</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithDangerousAcceptAnyServerCertificate(bool accept = true)
    {
        _httpClient = (_httpClient ?? new KyrolusHttpClientOptions()) with
        {
            DangerousAcceptAnyServerCertificate = accept
        };
        return this;
    }

    /// <summary>
    /// Sets the maximum number of concurrent HTTP/1.1 connections allowed per backend destination server.
    /// Defends against socket exhaustion and resource starvation.
    /// </summary>
    /// <param name="maxConnections">Maximum concurrent connections per destination.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithMaxConnectionsPerServer(int maxConnections)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConnections);
        _httpClient = (_httpClient ?? new KyrolusHttpClientOptions()) with
        {
            MaxConnectionsPerServer = maxConnections
        };
        return this;
    }

    /// <summary>
    /// Configures whether multiple HTTP/2 connections to the same backend server are permitted.
    /// Recommended for high-throughput gRPC and HTTP/2 microservices.
    /// </summary>
    /// <param name="enable">Whether multiple HTTP/2 connections are allowed. Defaults to <c>true</c>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder WithMultipleHttp2Connections(bool enable = true)
    {
        _httpClient = (_httpClient ?? new KyrolusHttpClientOptions()) with
        {
            EnableMultipleHttp2Connections = enable
        };
        return this;
    }

    /// <summary>
    /// Sets the HTTP request / activity timeout duration for outbound calls to backend destinations in this cluster.
    /// Alias for <see cref="WithTimeout(TimeSpan)"/>.
    /// </summary>
    public KyrolusClusterBuilder WithHttpRequestTimeout(TimeSpan timeout) => WithTimeout(timeout);

    /// <summary>
    /// Configures whether response bodies from backend destinations should be buffered before delivery to the client.
    /// Set to <c>false</c> for real-time streaming, WebSockets, Server-Sent Events (SSE), and large file downloads.
    /// </summary>
    public KyrolusClusterBuilder WithResponseBuffering(bool enable)
    {
        _allowResponseBuffering = enable;
        return this;
    }

    /// <summary>
    /// Configures whether response bodies from backend destinations should be buffered before delivery to the client.
    /// Set to <c>false</c> for real-time streaming, WebSockets, Server-Sent Events (SSE), and large file downloads.
    /// Alias for <see cref="WithResponseBuffering(bool)"/>.
    /// </summary>
    public KyrolusClusterBuilder WithAllowResponseBuffering(bool allow = true) => WithResponseBuffering(allow);

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
    /// <summary>
    /// Adds a child route belonging directly to this cluster with strongly-typed HTTP methods.
    /// </summary>
    /// <param name="routeId">The unique route identifier (e.g. <c>"get-orders-route"</c>).</param>
    /// <param name="path">The URL path pattern to match on incoming requests (e.g. <c>"/api/orders/{**catch-all}"</c>).</param>
    /// <param name="httpMethods">Optional list of allowed HTTP methods (use <see cref="KyrolusHttpMethod"/> properties). If omitted, matches all verbs.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder AddRoute(string routeId, string path, params KyrolusHttpMethod[] httpMethods)
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
                Methods = httpMethods is { Length: > 0 } ? httpMethods : null
            }
        });

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

        var normalizedMethods = httpMethods.Length > 0
            ? httpMethods.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => KyrolusHttpMethod.From(m)!.Value).ToArray()
            : null;

        _routes.Add(new KyrolusGatewayRoute
        {
            RouteId = routeId,
            ClusterId = _clusterId,
            Match = new KyrolusGatewayRouteMatch
            {
                Path = path,
                Methods = normalizedMethods is { Length: > 0 } ? normalizedMethods : null
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

        var normalizedMethods = methods?
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => KyrolusHttpMethod.From(m)!.Value)
            .ToList();

        var validatedHosts = hosts?
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => KyrolusHostValidator.Validate(h, nameof(hosts)))
            .ToList();

        _routes.Add(new KyrolusGatewayRoute
        {
            RouteId = routeId,
            ClusterId = _clusterId,
            Match = new KyrolusGatewayRouteMatch
            {
                Path = path,
                Methods = normalizedMethods is { Count: > 0 } ? normalizedMethods : null,
                Hosts = validatedHosts is { Count: > 0 } ? validatedHosts : null
            },
            Metadata = metadata
        });

        return this;
    }

    /// <summary>
    /// Adds a child route with advanced matching criteria (strongly-typed hosts, methods, custom metadata) belonging directly to this cluster.
    /// </summary>
    public KyrolusClusterBuilder AddRoute(
        string routeId,
        string path,
        IReadOnlyList<KyrolusHttpMethod>? methods,
        IReadOnlyList<KyrolusRouteHost>? hosts = null,
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
                Methods = methods is { Count: > 0 } ? methods : null,
                Hosts = hosts is { Count: > 0 } ? hosts.Select(h => h.Value).ToList() : null
            },
            Metadata = metadata
        });

        return this;
    }

    /// <summary>
    /// Adds a child route configured via a fluent <see cref="KyrolusRouteBuilder"/> delegate,
    /// enabling route-level security policies (Authorization, CORS, RateLimiting), timeouts, and URL transforms.
    /// </summary>
    /// <param name="routeId">The unique route identifier.</param>
    /// <param name="path">The URL path pattern to match on incoming requests.</param>
    /// <param name="configureRoute">A delegate configuring the <see cref="KyrolusRouteBuilder"/>.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KyrolusClusterBuilder AddRoute(string routeId, string path, Action<KyrolusRouteBuilder> configureRoute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(configureRoute);

        var builder = new KyrolusRouteBuilder(routeId, _clusterId, path);
        configureRoute(builder);
        _routes.Add(builder.Build());
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
            LoadBalancingPolicy = _loadBalancingPolicy,
            HealthCheck = _healthCheck,
            SessionAffinity = _sessionAffinity,
            HttpRequestTimeout = _httpRequestTimeout,
            HttpClient = _httpClient,
            AllowResponseBuffering = _allowResponseBuffering
        };

        return (cluster, _routes.AsReadOnly());
    }
}
