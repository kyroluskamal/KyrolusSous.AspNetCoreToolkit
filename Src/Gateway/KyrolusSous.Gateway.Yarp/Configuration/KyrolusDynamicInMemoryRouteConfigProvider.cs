namespace KyrolusSous.Gateway.Yarp.Configuration;

/// <summary>
/// Thread-safe in-memory configuration provider for YARP, supporting programmatic fluent cluster definition,
/// JSON configuration section loading, dynamic runtime route queries, and hot reloading via change tokens.
/// </summary>
/// <remarks>
/// <para>
/// <b>Architecture:</b><br/>
/// Implements both YARP's <see cref="IProxyConfigProvider"/> and the gateway contract <see cref="IKyrolusDynamicRouteProvider"/>.
/// Serves as the central repository of routes and clusters in memory, converting toolkit abstractions
/// (<see cref="KyrolusGatewayRoute"/> and <see cref="KyrolusGatewayCluster"/>) into YARP's native <see cref="RouteConfig"/> and <see cref="ClusterConfig"/>.
/// </para>
/// <para>
/// <b>Hot Reload &amp; Thread Safety:</b><br/>
/// Thread-safe against concurrent reads and writes using an internal synchronization lock.
/// Every configuration mutation signals YARP's reverse proxy engine to update routes and clusters with zero server downtime.
/// </para>
/// </remarks>
public sealed class KyrolusDynamicInMemoryRouteConfigProvider : IProxyConfigProvider, IKyrolusDynamicRouteProvider, IDisposable
{
    private readonly object _syncLock = new();
    private readonly Dictionary<string, KyrolusGatewayRoute> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KyrolusGatewayCluster> _clusters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _configRouteIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _configClusterIds = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource _changeTokenSource = new();
    private volatile KyrolusCustomProxyConfig _currentConfig;
    private IDisposable? _changeTokenSubscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusDynamicInMemoryRouteConfigProvider"/> class.
    /// </summary>
    public KyrolusDynamicInMemoryRouteConfigProvider()
    {
        _currentConfig = BuildConfigSnapshot(new CancellationChangeToken(_changeTokenSource.Token));
    }

    /// <summary>
    /// Adds or updates a raw gateway route rule in the in-memory provider and signals a reload.
    /// </summary>
    /// <param name="route">The gateway route to add or replace.</param>
    public void AddRoute(KyrolusGatewayRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        lock (_syncLock)
        {
            _routes[route.RouteId] = route;
            SignalChange();
        }
    }

    /// <summary>
    /// Adds or updates multiple raw gateway routes in a single atomic batch operation, triggering a single reload notification.
    /// </summary>
    /// <param name="routes">The collection of routes to add or replace.</param>
    public void AddRoutes(IEnumerable<KyrolusGatewayRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        lock (_syncLock)
        {
            foreach (var route in routes)
            {
                _routes[route.RouteId] = route;
            }
            SignalChange();
        }
    }

    /// <summary>
    /// Adds or updates a raw gateway service cluster with its destinations and load balancing policy, then signals a reload.
    /// </summary>
    /// <param name="cluster">The gateway cluster to add or replace.</param>
    public void AddCluster(KyrolusGatewayCluster cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        lock (_syncLock)
        {
            _clusters[cluster.ClusterId] = cluster;
            SignalChange();
        }
    }

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
        lock (_syncLock)
        {
            _clusters[cluster.ClusterId] = cluster;
            foreach (var r in routes)
            {
                _routes[r.RouteId] = r;
            }
            SignalChange();
        }

        return this;
    }

    /// <summary>
    /// Adds or updates multiple raw gateway clusters in a single atomic batch operation, triggering a single reload notification.
    /// </summary>
    /// <param name="clusters">The collection of clusters to add or replace.</param>
    public void AddClusters(IEnumerable<KyrolusGatewayCluster> clusters)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        lock (_syncLock)
        {
            foreach (var cluster in clusters)
            {
                _clusters[cluster.ClusterId] = cluster;
            }
            SignalChange();
        }
    }

    /// <summary>
    /// Loads routes and clusters from a configuration section (e.g., from appsettings.json <c>"ReverseProxy"</c> section)
    /// and automatically subscribes to configuration reload change tokens for live dynamic updates.
    /// </summary>
    /// <param name="section">The configuration section containing <c>Clusters</c> and <c>Routes</c> children.</param>
    /// <returns>The provider instance for fluent chaining.</returns>
    public KyrolusDynamicInMemoryRouteConfigProvider LoadFromConfiguration(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        _changeTokenSubscription?.Dispose();
        _changeTokenSubscription = ChangeToken.OnChange(
            section.GetReloadToken,
            () => ReloadFromConfiguration(section));

        ReloadFromConfiguration(section);
        return this;
    }

    private void ReloadFromConfiguration(IConfigurationSection section)
    {
        lock (_syncLock)
        {
            var clustersSection = section.GetSection("Clusters");
            if (!clustersSection.Exists())
                throw new InvalidOperationException($"Configuration section '{section.Path}:Clusters' is missing or empty. Cannot load clusters without a valid configuration.");

            // Remove previous configuration-sourced items that may have been changed or removed
            foreach (var rId in _configRouteIds)
            {
                _routes.Remove(rId);
            }
            _configRouteIds.Clear();

            foreach (var cId in _configClusterIds)
            {
                _clusters.Remove(cId);
            }
            _configClusterIds.Clear();

            LoadCluster(clustersSection.GetChildren());

            var routesSection = section.GetSection("Routes");
            if (routesSection.Exists())
            {
                LoadRoutes(routesSection.GetChildren());
            }
            else
            {
                // Only load sibling children that are actual route sections (contain ClusterId or Match)
                LoadRoutes(section.GetChildren().Where(s =>
                    !string.Equals(s.Key, "Clusters", StringComparison.OrdinalIgnoreCase) &&
                    (s.GetSection("ClusterId").Exists() || s.GetSection("Match").Exists())));
            }

            SignalChange();
        }
    }

    /// <summary>
    /// Builds and returns a snapshot of the current in-memory routes and clusters formatted as YARP's <see cref="IProxyConfig"/>.
    /// </summary>
    /// <returns>An instance of <see cref="KyrolusCustomProxyConfig"/> containing the active routes and clusters.</returns>
    public IProxyConfig GetConfig() => _currentConfig;

    /// <summary>
    /// Asynchronously retrieves the snapshot of all currently configured gateway routes.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of configured <see cref="KyrolusGatewayRoute"/> instances.</returns>
    public Task<IReadOnlyList<KyrolusGatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            return Task.FromResult<IReadOnlyList<KyrolusGatewayRoute>>(_routes.Values.ToList().AsReadOnly());
        }
    }

    /// <summary>
    /// Asynchronously retrieves the snapshot of all currently configured gateway clusters.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of configured <see cref="KyrolusGatewayCluster"/> instances.</returns>
    public Task<IReadOnlyList<KyrolusGatewayCluster>> GetClustersAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            return Task.FromResult<IReadOnlyList<KyrolusGatewayCluster>>(_clusters.Values.ToList().AsReadOnly());
        }
    }

    /// <summary>
    /// Signals the gateway engine to reload its configuration snapshot and notify YARP of any dynamic updates.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            SignalChange();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a gateway route by its identifier and signals a dynamic reload if found.
    /// </summary>
    /// <param name="routeId">The unique identifier of the route to remove.</param>
    /// <returns><c>true</c> if the route was removed; otherwise, <c>false</c>.</returns>
    public bool RemoveRoute(string routeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
        lock (_syncLock)
        {
            if (_routes.Remove(routeId))
            {
                _configRouteIds.Remove(routeId);
                SignalChange();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes a gateway cluster and all its associated child routes, then signals a dynamic reload if found.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the cluster to remove.</param>
    /// <returns><c>true</c> if the cluster was removed; otherwise, <c>false</c>.</returns>
    public bool RemoveCluster(string clusterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        lock (_syncLock)
        {
            if (_clusters.Remove(clusterId))
            {
                _configClusterIds.Remove(clusterId);
                var orphanedRoutes = _routes.Values
                    .Where(r => string.Equals(r.ClusterId, clusterId, StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.RouteId)
                    .ToList();

                foreach (var rId in orphanedRoutes)
                {
                    _routes.Remove(rId);
                    _configRouteIds.Remove(rId);
                }

                SignalChange();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes a specific backend destination endpoint from a cluster (node decommissioning / draining) and signals a reload.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the target cluster.</param>
    /// <param name="destinationId">The unique identifier of the destination node to remove.</param>
    /// <returns><c>true</c> if the destination was found and removed; otherwise, <c>false</c>.</returns>
    public bool RemoveDestination(string clusterId, string destinationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clusterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        lock (_syncLock)
        {
            if (_clusters.TryGetValue(clusterId, out var existingCluster))
            {
                if (existingCluster.Destinations.ContainsKey(destinationId))
                {
                    var updatedDestinations = existingCluster.Destinations
                        .Where(kv => !string.Equals(kv.Key, destinationId, StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

                    _clusters[clusterId] = existingCluster with { Destinations = updatedDestinations };
                    SignalChange();
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all routes and clusters from in-memory state and signals a dynamic reload.
    /// </summary>
    public void Clear()
    {
        lock (_syncLock)
        {
            _routes.Clear();
            _clusters.Clear();
            _configRouteIds.Clear();
            _configClusterIds.Clear();
            SignalChange();
        }
    }

    /// <inheritdoc />
    public Task<bool> RemoveRouteAsync(string routeId, CancellationToken cancellationToken = default)
        => Task.FromResult(RemoveRoute(routeId));

    /// <inheritdoc />
    public Task<bool> RemoveClusterAsync(string clusterId, CancellationToken cancellationToken = default)
        => Task.FromResult(RemoveCluster(clusterId));

    /// <inheritdoc />
    public Task AddRouteAsync(KyrolusGatewayRoute route, CancellationToken cancellationToken = default)
    {
        AddRoute(route);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddClusterAsync(KyrolusGatewayCluster cluster, CancellationToken cancellationToken = default)
    {
        AddCluster(cluster);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddRoutesAsync(IEnumerable<KyrolusGatewayRoute> routes, CancellationToken cancellationToken = default)
    {
        AddRoutes(routes);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddClustersAsync(IEnumerable<KyrolusGatewayCluster> clusters, CancellationToken cancellationToken = default)
    {
        AddClusters(clusters);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> RemoveDestinationAsync(string clusterId, string destinationId, CancellationToken cancellationToken = default)
        => Task.FromResult(RemoveDestination(clusterId, destinationId));

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Clear();
        return Task.CompletedTask;
    }

    private void SignalChange()
    {
        var oldCts = _changeTokenSource;
        var newCts = new CancellationTokenSource();
        _changeTokenSource = newCts;
        _currentConfig = BuildConfigSnapshot(new CancellationChangeToken(newCts.Token));
        oldCts.Cancel();
        oldCts.Dispose();
    }

    private KyrolusCustomProxyConfig BuildConfigSnapshot(IChangeToken changeToken)
    {
        var yarpRoutes = _routes.Values.Select(MapRoute).ToList();
        var yarpClusters = _clusters.Values.Select(MapCluster).ToList();
        return new KyrolusCustomProxyConfig(yarpRoutes, yarpClusters, changeToken);
    }

    private static RouteConfig MapRoute(KyrolusGatewayRoute r)
    {
        return new RouteConfig
        {
            RouteId = r.RouteId,
            ClusterId = r.ClusterId,
            Order = r.Order,
            Match = MapRouteMatch(r),
            Metadata = MapRouteMetadata(r),
            AuthorizationPolicy = r.AuthorizationPolicy,
            CorsPolicy = r.CorsPolicy,
            RateLimiterPolicy = r.RateLimiterPolicy,
            OutputCachePolicy = r.OutputCachePolicy,
            Timeout = r.Timeout,
            MaxRequestBodySize = r.MaxRequestBodySize,
            Transforms = r.Transforms?.Select(t => (IReadOnlyDictionary<string, string>)t).ToList()
        };
    }

    private static RouteMatch MapRouteMatch(KyrolusGatewayRoute r)
    {
        var normalizedMethods = r.Match.Methods?
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim().ToUpperInvariant())
            .ToList();

        var yarpHeaders = r.Match.Headers?.Select(h => new RouteHeader
        {
            Name = h.Name,
            Values = h.Values,
            Mode = ParseHeaderMatchMode(h.Mode),
            IsCaseSensitive = h.IsCaseSensitive
        }).ToList();

        var yarpQueryParams = r.Match.QueryParameters?.Select(q => new RouteQueryParameter
        {
            Name = q.Name,
            Values = q.Values,
            Mode = ParseQueryParamMatchMode(q.Mode),
            IsCaseSensitive = q.IsCaseSensitive
        }).ToList();

        return new RouteMatch
        {
            Path = r.Match.Path,
            Methods = normalizedMethods is { Count: > 0 } ? normalizedMethods : null,
            Hosts = r.Match.Hosts,
            Headers = yarpHeaders is { Count: > 0 } ? yarpHeaders : null,
            QueryParameters = yarpQueryParams is { Count: > 0 } ? yarpQueryParams : null
        };
    }

    private static Dictionary<string, string>? MapRouteMetadata(KyrolusGatewayRoute r)
    {
        var metadata = r.Metadata != null
            ? new Dictionary<string, string>(r.Metadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (r.IpFilter != null)
        {
            if (r.IpFilter.AllowedIpsOrCidrs is { Count: > 0 } allowed)
            {
                metadata["Kyrolus:IpFilter:Allowed"] = string.Join(",", allowed);
            }
            if (r.IpFilter.BlockedIpsOrCidrs is { Count: > 0 } blocked)
            {
                metadata["Kyrolus:IpFilter:Blocked"] = string.Join(",", blocked);
            }
        }

        if (r.RequireTenant)
        {
            metadata["Kyrolus:Tenant:Required"] = "true";
        }

        if (r.AllowedContentTypes is { Count: > 0 } allowedTypes)
        {
            metadata["Kyrolus:ContentType:Allowed"] = string.Join(",", allowedTypes);
        }

        return metadata.Count > 0 ? metadata : null;
    }

    private static ClusterConfig MapCluster(KyrolusGatewayCluster c)
    {
        return new ClusterConfig
        {
            ClusterId = c.ClusterId,
            LoadBalancingPolicy = c.LoadBalancingPolicy,
            Destinations = c.Destinations.ToDictionary(
                kv => kv.Key,
                kv => new DestinationConfig { Address = kv.Value.Address }),
            HealthCheck = MapHealthCheck(c.HealthCheck),
            SessionAffinity = MapSessionAffinity(c.SessionAffinity),
            HttpClient = MapHttpClient(c.HttpClient),
            HttpRequest = MapForwarderRequest(c)
        };
    }

    private static HealthCheckConfig? MapHealthCheck(KyrolusHealthCheckOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new HealthCheckConfig
        {
            Active = MapActiveHealthCheck(options.Active),
            Passive = MapPassiveHealthCheck(options.Passive),
            AvailableDestinationsPolicy = options.AvailableDestinationsPolicy
        };
    }

    private static ActiveHealthCheckConfig? MapActiveHealthCheck(KyrolusActiveHealthCheckOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new ActiveHealthCheckConfig
        {
            Enabled = options.Enabled,
            Interval = options.Interval,
            Timeout = options.Timeout,
            Policy = options.Policy,
            Path = NormalizeHealthCheckPath(options.Path)
        };
    }

    private static PassiveHealthCheckConfig? MapPassiveHealthCheck(KyrolusPassiveHealthCheckOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new PassiveHealthCheckConfig
        {
            Enabled = options.Enabled,
            Policy = options.Policy,
            ReactivationPeriod = options.ReactivationPeriod
        };
    }

    private static SessionAffinityConfig? MapSessionAffinity(KyrolusSessionAffinityOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new SessionAffinityConfig
        {
            Enabled = options.Enabled,
            Policy = options.Policy,
            FailurePolicy = options.FailurePolicy,
            AffinityKeyName = options.AffinityKeyName ?? ".KyrolusGateway.Affinity",
            Cookie = MapSessionAffinityCookie(options.Cookie)
        };
    }

    private static HttpClientConfig? MapHttpClient(KyrolusHttpClientOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        return new HttpClientConfig
        {
            DangerousAcceptAnyServerCertificate = options.DangerousAcceptAnyServerCertificate,
            MaxConnectionsPerServer = options.MaxConnectionsPerServer,
            EnableMultipleHttp2Connections = options.EnableMultipleHttp2Connections
        };
    }

    private static ForwarderRequestConfig MapForwarderRequest(KyrolusGatewayCluster c)
    {
        return new ForwarderRequestConfig
        {
            ActivityTimeout = c.HttpRequestTimeout ?? TimeSpan.FromSeconds(60),
            AllowResponseBuffering = c.AllowResponseBuffering,
            Version = c.HttpClient?.DefaultVersion,
            VersionPolicy = c.HttpClient?.VersionPolicy
        };
    }

    private void LoadCluster(IEnumerable<IConfigurationSection> configurationSections)
    {
        foreach (var clusterSec in configurationSections)
        {
            var cluster = ParseCluster(clusterSec);
            _clusters[cluster.ClusterId] = cluster;
            _configClusterIds.Add(cluster.ClusterId);
        }
    }

    private static KyrolusGatewayCluster ParseCluster(IConfigurationSection clusterSec)
    {
        var clusterId = clusterSec.Key;
        var destinations = ParseDestinations(clusterSec.GetSection("Destinations"));
        var healthCheck = ParseHealthCheck(clusterSec.GetSection("HealthCheck"));
        var sessionAffinity = ParseSessionAffinity(clusterSec.GetSection("SessionAffinity"));
        var httpRequestSec = clusterSec.GetSection("HttpRequest");
        var httpTimeout = ParseHttpTimeout(clusterSec, httpRequestSec);
        var allowBuffering = bool.TryParse(clusterSec["AllowResponseBuffering"], out var arb) ? arb : (bool?)null;
        var httpClientOptions = ParseHttpClient(clusterSec.GetSection("HttpClient"), httpRequestSec);

        return new KyrolusGatewayCluster
        {
            ClusterId = clusterId,
            Destinations = destinations,
            LoadBalancingPolicy = KyrolusLoadBalancingPolicy.From(clusterSec["LoadBalancingPolicy"]),
            HealthCheck = healthCheck,
            SessionAffinity = sessionAffinity,
            HttpRequestTimeout = httpTimeout,
            HttpClient = httpClientOptions,
            AllowResponseBuffering = allowBuffering
        };
    }

    private static Dictionary<string, KyrolusGatewayDestination> ParseDestinations(IConfigurationSection destinationsSec)
    {
        var destinations = new Dictionary<string, KyrolusGatewayDestination>(StringComparer.OrdinalIgnoreCase);
        foreach (var destSec in destinationsSec.GetChildren())
        {
            var address = destSec["Address"];
            if (!string.IsNullOrWhiteSpace(address))
            {
                destinations[destSec.Key] = new KyrolusGatewayDestination(address);
            }
        }
        return destinations;
    }

    private static KyrolusHealthCheckOptions? ParseHealthCheck(IConfigurationSection healthCheckSec)
    {
        if (!healthCheckSec.Exists())
        {
            return null;
        }

        KyrolusActiveHealthCheckOptions? active = null;
        var activeSec = healthCheckSec.GetSection("Active");
        if (activeSec.Exists())
        {
            active = new KyrolusActiveHealthCheckOptions
            {
                Enabled = bool.TryParse(activeSec["Enabled"], out var en) && en,
                Interval = TimeSpan.TryParse(activeSec["Interval"], CultureInfo.InvariantCulture, out var iv) ? iv : null,
                Timeout = TimeSpan.TryParse(activeSec["Timeout"], CultureInfo.InvariantCulture, out var to) ? to : null,
                Policy = KyrolusActiveHealthCheckPolicy.From(activeSec["Policy"]) ?? KyrolusActiveHealthCheckPolicy.ConsecutiveFailures,
                Path = activeSec["Path"]
            };
        }

        KyrolusPassiveHealthCheckOptions? passive = null;
        var passiveSec = healthCheckSec.GetSection("Passive");
        if (passiveSec.Exists())
        {
            passive = new KyrolusPassiveHealthCheckOptions
            {
                Enabled = bool.TryParse(passiveSec["Enabled"], out var pen) && pen,
                Policy = KyrolusPassiveHealthCheckPolicy.From(passiveSec["Policy"]) ?? KyrolusPassiveHealthCheckPolicy.TransportFailureRate,
                ReactivationPeriod = TimeSpan.TryParse(passiveSec["ReactivationPeriod"], CultureInfo.InvariantCulture, out var rp) ? rp : null
            };
        }

        return new KyrolusHealthCheckOptions
        {
            Active = active,
            Passive = passive,
            AvailableDestinationsPolicy = KyrolusAvailableDestinationsPolicy.From(healthCheckSec["AvailableDestinationsPolicy"]) ?? KyrolusAvailableDestinationsPolicy.HealthyOrUnspecified
        };
    }

    private static KyrolusSessionAffinityOptions? ParseSessionAffinity(IConfigurationSection affinitySec)
    {
        if (!affinitySec.Exists())
        {
            return null;
        }

        KyrolusSessionAffinityCookieOptions? cookieOptions = null;
        var cookieSec = affinitySec.GetSection("Cookie");
        if (cookieSec.Exists())
        {
            cookieOptions = new KyrolusSessionAffinityCookieOptions
            {
                Path = cookieSec["Path"] ?? "/",
                Domain = cookieSec["Domain"],
                HttpOnly = !bool.TryParse(cookieSec["HttpOnly"], out var ho) || ho,
                SecurePolicy = cookieSec["SecurePolicy"] ?? "SameAsRequest",
                SameSite = cookieSec["SameSite"] ?? "Lax",
                Expiration = TimeSpan.TryParse(cookieSec["Expiration"], CultureInfo.InvariantCulture, out var exp) ? exp : null,
                MaxAge = TimeSpan.TryParse(cookieSec["MaxAge"], CultureInfo.InvariantCulture, out var ma) ? ma : null,
                IsEssential = !bool.TryParse(cookieSec["IsEssential"], out var ess) || ess
            };
        }

        return new KyrolusSessionAffinityOptions
        {
            Enabled = bool.TryParse(affinitySec["Enabled"], out var aen) && aen,
            Policy = affinitySec["Policy"],
            FailurePolicy = affinitySec["FailurePolicy"],
            AffinityKeyName = affinitySec["AffinityKeyName"],
            Cookie = cookieOptions
        };
    }

    private static TimeSpan? ParseHttpTimeout(IConfigurationSection clusterSec, IConfigurationSection httpRequestSec)
    {
        if (httpRequestSec.Exists() && TimeSpan.TryParse(httpRequestSec["Timeout"], CultureInfo.InvariantCulture, out var toVal))
        {
            return toVal;
        }
        if (TimeSpan.TryParse(clusterSec["HttpRequestTimeout"], CultureInfo.InvariantCulture, out TimeSpan directTo))
        {
            return directTo;
        }
        return null;
    }

    private static KyrolusHttpClientOptions? ParseHttpClient(IConfigurationSection httpClientSec, IConfigurationSection httpRequestSec)
    {
        var (defaultVer, vPolicy) = ResolveHttpVersionAndPolicy(httpClientSec, httpRequestSec);

        if (!httpClientSec.Exists() && defaultVer is null && vPolicy is null)
        {
            return null;
        }

        return CreateHttpClientOptions(httpClientSec, defaultVer, vPolicy);
    }

    private static (Version? Version, HttpVersionPolicy? Policy) ResolveHttpVersionAndPolicy(
        IConfigurationSection httpClientSec,
        IConfigurationSection httpRequestSec)
    {
        var version = TryParseVersion(httpClientSec["DefaultVersion"]) ?? TryParseVersion(httpRequestSec["Version"]);
        var policy = TryParsePolicy(httpClientSec["VersionPolicy"]) ?? TryParsePolicy(httpRequestSec["VersionPolicy"]);
        return (version, policy);
    }

    private static Version? TryParseVersion(string? value) =>
        Version.TryParse(value, out var v) ? v : null;

    private static HttpVersionPolicy? TryParsePolicy(string? value) =>
        Enum.TryParse<HttpVersionPolicy>(value, true, out var p) ? p : null;

    private static KyrolusHttpClientOptions CreateHttpClientOptions(
        IConfigurationSection httpClientSec,
        Version? defaultVer,
        HttpVersionPolicy? vPolicy)
    {
        return new KyrolusHttpClientOptions
        {
            DangerousAcceptAnyServerCertificate = bool.TryParse(httpClientSec["DangerousAcceptAnyServerCertificate"], out var sc) && sc,
            MaxConnectionsPerServer = int.TryParse(httpClientSec["MaxConnectionsPerServer"], CultureInfo.InvariantCulture, out var mc) ? mc : null,
            EnableMultipleHttp2Connections = bool.TryParse(httpClientSec["EnableMultipleHttp2Connections"], out var em) ? em : null,
            DefaultVersion = defaultVer,
            VersionPolicy = vPolicy
        };
    }

    private static string? NormalizeHealthCheckPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var trimmed = path.Trim();
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }

    private static SessionAffinityCookieConfig MapSessionAffinityCookie(KyrolusSessionAffinityCookieOptions? options)
    {
        var cookie = options ?? new KyrolusSessionAffinityCookieOptions();
        return new SessionAffinityCookieConfig
        {
            Path = cookie.Path,
            Domain = cookie.Domain,
            HttpOnly = cookie.HttpOnly,
            SecurePolicy = ParseCookieSecurePolicy(cookie.SecurePolicy),
            SameSite = ParseSameSiteMode(cookie.SameSite),
            Expiration = cookie.Expiration,
            MaxAge = cookie.MaxAge,
            IsEssential = cookie.IsEssential
        };
    }

    private static CookieSecurePolicy ParseCookieSecurePolicy(string? value)
    {
        if (string.Equals(value, "Always", StringComparison.OrdinalIgnoreCase))
            return CookieSecurePolicy.Always;
        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            return CookieSecurePolicy.None;
        return CookieSecurePolicy.SameAsRequest;
    }

    private static SameSiteMode ParseSameSiteMode(string? value)
    {
        if (string.Equals(value, "Strict", StringComparison.OrdinalIgnoreCase))
            return SameSiteMode.Strict;
        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            return SameSiteMode.None;
        if (string.Equals(value, "Unspecified", StringComparison.OrdinalIgnoreCase))
            return SameSiteMode.Unspecified;
        return SameSiteMode.Lax;
    }

    private static HeaderMatchMode ParseHeaderMatchMode(string? mode)
    {
        if (string.Equals(mode, "HeaderPrefix", StringComparison.OrdinalIgnoreCase))
            return HeaderMatchMode.HeaderPrefix;
        if (string.Equals(mode, "Exists", StringComparison.OrdinalIgnoreCase))
            return HeaderMatchMode.Exists;
        if (string.Equals(mode, "NotExists", StringComparison.OrdinalIgnoreCase))
            return HeaderMatchMode.NotExists;
        return HeaderMatchMode.ExactHeader;
    }

    private static QueryParameterMatchMode ParseQueryParamMatchMode(string? mode)
    {
        if (string.Equals(mode, "Prefix", StringComparison.OrdinalIgnoreCase))
            return QueryParameterMatchMode.Prefix;
        if (string.Equals(mode, "Exists", StringComparison.OrdinalIgnoreCase))
            return QueryParameterMatchMode.Exists;
        if (string.Equals(mode, "Contains", StringComparison.OrdinalIgnoreCase))
            return QueryParameterMatchMode.Contains;
        if (string.Equals(mode, "NotContains", StringComparison.OrdinalIgnoreCase))
            return QueryParameterMatchMode.NotContains;
        return QueryParameterMatchMode.Exact;
    }

    private void LoadRoutes(IEnumerable<IConfigurationSection> routesSection)
    {
        foreach (var routeSec in routesSection)
        {
            var route = ParseRoute(routeSec);
            _routes[route.RouteId] = route;
            _configRouteIds.Add(route.RouteId);
        }
    }

    private static KyrolusGatewayRoute ParseRoute(IConfigurationSection routeSec)
    {
        var routeId = routeSec.Key;
        var clusterId = routeSec["ClusterId"] ?? string.Empty;
        var matchSec = routeSec.GetSection("Match");
        var path = matchSec["Path"] ?? string.Empty;

        var methods = matchSec.GetSection("Methods").GetChildren().Select(c => c.Value).OfType<string>().ToList();
        var hosts = matchSec.GetSection("Hosts").GetChildren().Select(c => c.Value).OfType<string>().ToList();
        var headers = ParseRouteHeaders(matchSec.GetSection("Headers"));
        var queryParams = ParseRouteQueryParams(matchSec.GetSection("QueryParameters"));

        var metadataSec = routeSec.GetSection("Metadata");
        var metadata = metadataSec.GetChildren().ToDictionary(c => c.Key, c => c.Value ?? string.Empty);

        var timeout = TimeSpan.TryParse(routeSec["Timeout"], CultureInfo.InvariantCulture, out var parsedTimeout) ? parsedTimeout : (TimeSpan?)null;
        var maxRequestBodySize = long.TryParse(routeSec["MaxRequestBodySize"], CultureInfo.InvariantCulture, out var parsedSize) ? parsedSize : (long?)null;
        var order = int.TryParse(routeSec["Order"], CultureInfo.InvariantCulture, out var parsedOrder) ? parsedOrder : (int?)null;
        var requireTenant = bool.TryParse(routeSec["RequireTenant"], out var parsedRt) && parsedRt;
        var allowedContentTypes = routeSec.GetSection("AllowedContentTypes").GetChildren().Select(c => c.Value).OfType<string>().ToList();

        var ipFilter = ParseIpFilter(routeSec.GetSection("IpFilter"));
        var transformsList = ParseTransforms(routeSec.GetSection("Transforms"));

        return new KyrolusGatewayRoute
        {
            RouteId = routeId,
            ClusterId = clusterId,
            Match = new KyrolusGatewayRouteMatch
            {
                Path = path,
                Methods = methods.Count > 0 ? methods : null,
                Hosts = hosts.Count > 0 ? hosts : null,
                Headers = headers,
                QueryParameters = queryParams
            },
            Metadata = metadata.Count > 0 ? metadata : null,
            AuthorizationPolicy = KyrolusAuthorizationPolicy.From(routeSec["AuthorizationPolicy"]),
            CorsPolicy = KyrolusCorsPolicy.From(routeSec["CorsPolicy"]),
            RateLimiterPolicy = KyrolusRateLimiterPolicy.From(routeSec["RateLimiterPolicy"]),
            OutputCachePolicy = KyrolusOutputCachePolicy.From(routeSec["OutputCachePolicy"]),
            Timeout = timeout,
            MaxRequestBodySize = maxRequestBodySize,
            Order = order,
            RequireTenant = requireTenant,
            AllowedContentTypes = allowedContentTypes.Count > 0 ? allowedContentTypes : null,
            IpFilter = ipFilter,
            Transforms = transformsList?.Select(KyrolusGatewayTransform.From).ToList()
        };
    }

    private static List<KyrolusRouteHeader>? ParseRouteHeaders(IConfigurationSection headersSec)
    {
        if (!headersSec.Exists())
        {
            return null;
        }

        var headers = new List<KyrolusRouteHeader>();
        foreach (var hItem in headersSec.GetChildren())
        {
            var name = hItem["Name"] ?? hItem.Key;
            var values = hItem.GetSection("Values").GetChildren().Select(c => c.Value).OfType<string>().ToList();
            var mode = hItem["Mode"] ?? "ExactHeader";
            var isCaseSensitive = bool.TryParse(hItem["IsCaseSensitive"], out var cs) && cs;
            headers.Add(new KyrolusRouteHeader
            {
                Name = name,
                Values = values.Count > 0 ? values : null,
                Mode = mode,
                IsCaseSensitive = isCaseSensitive
            });
        }
        return headers;
    }

    private static List<KyrolusRouteQueryParameter>? ParseRouteQueryParams(IConfigurationSection queryParamsSec)
    {
        if (!queryParamsSec.Exists())
        {
            return null;
        }

        var queryParams = new List<KyrolusRouteQueryParameter>();
        foreach (var qItem in queryParamsSec.GetChildren())
        {
            var name = qItem["Name"] ?? qItem.Key;
            var values = qItem.GetSection("Values").GetChildren().Select(c => c.Value).OfType<string>().ToList();
            var mode = qItem["Mode"] ?? "Exact";
            var isCaseSensitive = bool.TryParse(qItem["IsCaseSensitive"], out var cs) && cs;
            queryParams.Add(new KyrolusRouteQueryParameter
            {
                Name = name,
                Values = values.Count > 0 ? values : null,
                Mode = mode,
                IsCaseSensitive = isCaseSensitive
            });
        }
        return queryParams;
    }

    private static KyrolusIpFilterOptions? ParseIpFilter(IConfigurationSection ipFilterSec)
    {
        if (!ipFilterSec.Exists())
        {
            return null;
        }

        var allowed = ipFilterSec.GetSection("AllowedIpsOrCidrs").GetChildren().Select(c => c.Value).OfType<string>().ToList();
        var blocked = ipFilterSec.GetSection("BlockedIpsOrCidrs").GetChildren().Select(c => c.Value).OfType<string>().ToList();
        if (allowed.Count == 0 && blocked.Count == 0)
        {
            return null;
        }

        return new KyrolusIpFilterOptions
        {
            AllowedIpsOrCidrs = allowed.Count > 0 ? allowed : null,
            BlockedIpsOrCidrs = blocked.Count > 0 ? blocked : null
        };
    }

    private static List<IReadOnlyDictionary<string, string>>? ParseTransforms(IConfigurationSection transformsSec)
    {
        if (!transformsSec.Exists())
        {
            return null;
        }

        var transformsList = new List<IReadOnlyDictionary<string, string>>();
        foreach (var transformItem in transformsSec.GetChildren())
        {
            var dict = transformItem.GetChildren().ToDictionary(c => c.Key, c => c.Value ?? string.Empty);
            if (dict.Count > 0)
            {
                transformsList.Add(dict);
            }
        }
        return transformsList.Count > 0 ? transformsList : null;
    }

    /// <summary>
    /// Releases all managed resources used by the configuration provider.
    /// </summary>
    public void Dispose()
    {
        _changeTokenSubscription?.Dispose();
        _changeTokenSource.Dispose();
    }
}
