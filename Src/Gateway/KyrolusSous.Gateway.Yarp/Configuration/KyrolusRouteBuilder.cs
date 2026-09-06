namespace KyrolusSous.Gateway.Yarp.Configuration;

/// <summary>
/// Fluent builder for configuring individual route matching, security policies, timeouts, and URL transforms.
/// </summary>
public sealed class KyrolusRouteBuilder
{
    private readonly string _routeId;
    private readonly string _clusterId;
    private readonly string _path;
    private readonly List<string> _methods = [];
    private readonly List<string> _hosts = [];
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IReadOnlyDictionary<string, string>> _transforms = [];
    private string? _authorizationPolicy;
    private string? _corsPolicy;
    private string? _rateLimiterPolicy;
    private TimeSpan? _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusRouteBuilder"/> class.
    /// </summary>
    public KyrolusRouteBuilder(string routeId, string clusterId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _routeId = routeId;
        _clusterId = clusterId;
        _path = path;
    }

    /// <summary>
    /// Restricts this route to the specified HTTP methods.
    /// Use constants from <see cref="KyrolusGatewayHttpMethods"/>.
    /// </summary>
    public KyrolusRouteBuilder WithMethods(params string[] methods)
    {
        if (methods is { Length: > 0 })
        {
            _methods.AddRange(methods);
        }
        return this;
    }

    /// <summary>
    /// Restricts this route to the specified incoming client request hostnames / domains (e.g., <c>""api.example.com""</c>).
    /// </summary>
    public KyrolusRouteBuilder WithHosts(params string[] hosts)
    {
        if (hosts is { Length: > 0 })
        {
            _hosts.AddRange(hosts);
        }
        return this;
    }

    /// <summary>
    /// Enforces an ASP.NET Core authorization policy on this route at the gateway edge.
    /// </summary>
    public KyrolusRouteBuilder WithAuthorization(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _authorizationPolicy = policy;
        return this;
    }

    /// <summary>
    /// Enforces an ASP.NET Core CORS policy on this route.
    /// </summary>
    public KyrolusRouteBuilder WithCors(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _corsPolicy = policy;
        return this;
    }

    /// <summary>
    /// Enforces an ASP.NET Core rate limiter policy on this route at the gateway perimeter.
    /// </summary>
    public KyrolusRouteBuilder WithRateLimiter(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);
        _rateLimiterPolicy = policy;
        return this;
    }

    /// <summary>
    /// Sets a processing timeout for requests matching this route.
    /// </summary>
    public KyrolusRouteBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Adds a path transform that strips the specified prefix from the request URL before forwarding to the backend.
    /// E.g. <c>""/api/orders/123""</c> with prefix <c>""/api""</c> becomes <c>""/orders/123""</c>.
    /// </summary>
    public KyrolusRouteBuilder WithTransformPathRemovePrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _transforms.Add(new Dictionary<string, string> { ["PathRemovePrefix"] = prefix });
        return this;
    }

    /// <summary>
    /// Adds a path transform that prepends the specified prefix to the request URL before forwarding to the backend.
    /// </summary>
    public KyrolusRouteBuilder WithTransformPathPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        _transforms.Add(new Dictionary<string, string> { ["PathPrefix"] = prefix });
        return this;
    }

    /// <summary>
    /// Adds a path transform that replaces the request URL with the specified fixed path.
    /// </summary>
    public KyrolusRouteBuilder WithTransformPathSet(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _transforms.Add(new Dictionary<string, string> { ["PathSet"] = path });
        return this;
    }

    /// <summary>
    /// Attaches custom metadata to the route.
    /// </summary>
    public KyrolusRouteBuilder WithMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _metadata[key] = value ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured <see cref="KyrolusGatewayRoute"/> instance.
    /// </summary>
    public KyrolusGatewayRoute Build()
    {
        return new KyrolusGatewayRoute
        {
            RouteId = _routeId,
            ClusterId = _clusterId,
            Match = new KyrolusGatewayRouteMatch
            {
                Path = _path,
                Methods = _methods.Count > 0 ? _methods.AsReadOnly() : null,
                Hosts = _hosts.Count > 0 ? _hosts.AsReadOnly() : null
            },
            AuthorizationPolicy = _authorizationPolicy,
            CorsPolicy = _corsPolicy,
            RateLimiterPolicy = _rateLimiterPolicy,
            Timeout = _timeout,
            Transforms = _transforms.Count > 0 ? _transforms.AsReadOnly() : null,
            Metadata = _metadata.Count > 0 ? _metadata : null
        };
    }
}
