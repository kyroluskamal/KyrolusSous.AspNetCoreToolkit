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
        var yarpRoutes = _routes.Values.Select(r =>
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

            return new RouteConfig
            {
                RouteId = r.RouteId,
                ClusterId = r.ClusterId,
                Order = r.Order,
                Match = new RouteMatch
                {
                    Path = r.Match.Path,
                    Methods = normalizedMethods is { Count: > 0 } ? normalizedMethods : null,
                    Hosts = r.Match.Hosts,
                    Headers = yarpHeaders is { Count: > 0 } ? yarpHeaders : null,
                    QueryParameters = yarpQueryParams is { Count: > 0 } ? yarpQueryParams : null
                },
                Metadata = metadata.Count > 0 ? metadata : null,
                AuthorizationPolicy = r.AuthorizationPolicy,
                CorsPolicy = r.CorsPolicy,
                RateLimiterPolicy = r.RateLimiterPolicy,
                OutputCachePolicy = r.OutputCachePolicy,
                Timeout = r.Timeout,
                MaxRequestBodySize = r.MaxRequestBodySize,
                Transforms = r.Transforms
            };
        }).ToList();

        var yarpClusters = _clusters.Values.Select(c => new ClusterConfig
        {
            ClusterId = c.ClusterId,
            LoadBalancingPolicy = c.LoadBalancingPolicy,
            Destinations = c.Destinations.ToDictionary(
                kv => kv.Key,
                kv => new DestinationConfig { Address = kv.Value.Address }),
            HealthCheck = c.HealthCheck is null ? null : new HealthCheckConfig
            {
                Active = c.HealthCheck.Active is null ? null : new ActiveHealthCheckConfig
                {
                    Enabled = c.HealthCheck.Active.Enabled,
                    Interval = c.HealthCheck.Active.Interval,
                    Timeout = c.HealthCheck.Active.Timeout,
                    Policy = c.HealthCheck.Active.Policy,
                    Path = NormalizeHealthCheckPath(c.HealthCheck.Active.Path)
                },
                Passive = c.HealthCheck.Passive is null ? null : new PassiveHealthCheckConfig
                {
                    Enabled = c.HealthCheck.Passive.Enabled,
                    Policy = c.HealthCheck.Passive.Policy,
                    ReactivationPeriod = c.HealthCheck.Passive.ReactivationPeriod
                },
                AvailableDestinationsPolicy = c.HealthCheck.AvailableDestinationsPolicy
            },
            SessionAffinity = c.SessionAffinity is null ? null : new SessionAffinityConfig
            {
                Enabled = c.SessionAffinity.Enabled,
                Policy = c.SessionAffinity.Policy,
                FailurePolicy = c.SessionAffinity.FailurePolicy,
                AffinityKeyName = c.SessionAffinity.AffinityKeyName ?? ".KyrolusGateway.Affinity",
                Cookie = MapSessionAffinityCookie(c.SessionAffinity.Cookie)
            },
            HttpClient = c.HttpClient is null ? null : new HttpClientConfig
            {
                DangerousAcceptAnyServerCertificate = c.HttpClient.DangerousAcceptAnyServerCertificate,
                MaxConnectionsPerServer = c.HttpClient.MaxConnectionsPerServer,
                EnableMultipleHttp2Connections = c.HttpClient.EnableMultipleHttp2Connections
            },
            HttpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = c.HttpRequestTimeout ?? TimeSpan.FromSeconds(60),
                AllowResponseBuffering = c.AllowResponseBuffering,
                Version = c.HttpClient?.DefaultVersion,
                VersionPolicy = c.HttpClient?.VersionPolicy
            }
        }).ToList();

        return new KyrolusCustomProxyConfig(yarpRoutes, yarpClusters, changeToken);
    }

    private void LoadCluster(IEnumerable<IConfigurationSection> configurationSections)
    {
        foreach (var clusterSec in configurationSections)
        {
            var clusterId = clusterSec.Key;
            var loadBalancingPolicy = clusterSec["LoadBalancingPolicy"];
            var destinations = new Dictionary<string, KyrolusGatewayDestination>(StringComparer.OrdinalIgnoreCase);

            var destinationsSec = clusterSec.GetSection("Destinations");
            foreach (var destSec in destinationsSec.GetChildren())
            {
                var address = destSec["Address"];
                if (!string.IsNullOrWhiteSpace(address))
                    destinations[destSec.Key] = new KyrolusGatewayDestination(address);
            }

            KyrolusHealthCheckOptions? healthCheck = null;
            var healthCheckSec = clusterSec.GetSection("HealthCheck");
            if (healthCheckSec.Exists())
            {
                KyrolusActiveHealthCheckOptions? active = null;
                var activeSec = healthCheckSec.GetSection("Active");
                if (activeSec.Exists())
                {
                    active = new KyrolusActiveHealthCheckOptions
                    {
                        Enabled = bool.TryParse(activeSec["Enabled"], out var en) && en,
                        Interval = TimeSpan.TryParse(activeSec["Interval"], out var iv) ? iv : null,
                        Timeout = TimeSpan.TryParse(activeSec["Timeout"], out var to) ? to : null,
                        Policy = activeSec["Policy"],
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
                        Policy = passiveSec["Policy"],
                        ReactivationPeriod = TimeSpan.TryParse(passiveSec["ReactivationPeriod"], out var rp) ? rp : null
                    };
                }

                healthCheck = new KyrolusHealthCheckOptions
                {
                    Active = active,
                    Passive = passive,
                    AvailableDestinationsPolicy = healthCheckSec["AvailableDestinationsPolicy"]
                };
            }

            KyrolusSessionAffinityOptions? sessionAffinity = null;
            var affinitySec = clusterSec.GetSection("SessionAffinity");
            if (affinitySec.Exists())
            {
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
                        Expiration = TimeSpan.TryParse(cookieSec["Expiration"], out var exp) ? exp : null,
                        MaxAge = TimeSpan.TryParse(cookieSec["MaxAge"], out var ma) ? ma : null,
                        IsEssential = !bool.TryParse(cookieSec["IsEssential"], out var ess) || ess
                    };
                }

                sessionAffinity = new KyrolusSessionAffinityOptions
                {
                    Enabled = bool.TryParse(affinitySec["Enabled"], out var aen) && aen,
                    Policy = affinitySec["Policy"],
                    FailurePolicy = affinitySec["FailurePolicy"],
                    AffinityKeyName = affinitySec["AffinityKeyName"],
                    Cookie = cookieOptions
                };
            }

            TimeSpan? httpTimeout = null;
            var httpRequestSec = clusterSec.GetSection("HttpRequest");
            if (httpRequestSec.Exists() && TimeSpan.TryParse(httpRequestSec["Timeout"], out var toVal))
            {
                httpTimeout = toVal;
            }
            else if (TimeSpan.TryParse(clusterSec["HttpRequestTimeout"], out TimeSpan directTo))
            {
                httpTimeout = directTo;
            }

            var allowBuffering = bool.TryParse(clusterSec["AllowResponseBuffering"], out var arb) ? arb : (bool?)null;

            Version? defaultVer = null;
            HttpVersionPolicy? vPolicy = null;

            var httpClientSec = clusterSec.GetSection("HttpClient");
            if (httpClientSec.Exists())
            {
                if (Version.TryParse(httpClientSec["DefaultVersion"], out var pv))
                {
                    defaultVer = pv;
                }

                if (Enum.TryParse<HttpVersionPolicy>(httpClientSec["VersionPolicy"], true, out var pp))
                {
                    vPolicy = pp;
                }
            }

            if (httpRequestSec.Exists())
            {
                if (defaultVer == null && Version.TryParse(httpRequestSec["Version"], out var pv))
                {
                    defaultVer = pv;
                }

                if (vPolicy == null && Enum.TryParse<HttpVersionPolicy>(httpRequestSec["VersionPolicy"], true, out var pp))
                {
                    vPolicy = pp;
                }
            }

            KyrolusHttpClientOptions? httpClientOptions = null;
            if (httpClientSec.Exists() || defaultVer != null || vPolicy != null)
            {
                httpClientOptions = new KyrolusHttpClientOptions
                {
                    DangerousAcceptAnyServerCertificate = bool.TryParse(httpClientSec["DangerousAcceptAnyServerCertificate"], out var sc) && sc,
                    MaxConnectionsPerServer = int.TryParse(httpClientSec["MaxConnectionsPerServer"], out var mc) ? mc : null,
                    EnableMultipleHttp2Connections = bool.TryParse(httpClientSec["EnableMultipleHttp2Connections"], out var em) ? em : null,
                    DefaultVersion = defaultVer,
                    VersionPolicy = vPolicy
                };
            }

            _clusters[clusterId] = new KyrolusGatewayCluster
            {
                ClusterId = clusterId,
                Destinations = destinations,
                LoadBalancingPolicy = loadBalancingPolicy,
                HealthCheck = healthCheck,
                SessionAffinity = sessionAffinity,
                HttpRequestTimeout = httpTimeout,
                HttpClient = httpClientOptions,
                AllowResponseBuffering = allowBuffering
            };
            _configClusterIds.Add(clusterId);
        }
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
            var routeId = routeSec.Key;
            var clusterId = routeSec["ClusterId"] ?? string.Empty;
            var matchSec = routeSec.GetSection("Match");
            var path = matchSec["Path"] ?? string.Empty;

            var methods = matchSec.GetSection("Methods").GetChildren().Select(c => c.Value).OfType<string>().ToList();
            var hosts = matchSec.GetSection("Hosts").GetChildren().Select(c => c.Value).OfType<string>().ToList();

            List<KyrolusRouteHeader>? headers = null;
            var headersSec = matchSec.GetSection("Headers");
            if (headersSec.Exists())
            {
                headers = [];
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
            }

            List<KyrolusRouteQueryParameter>? queryParams = null;
            var queryParamsSec = matchSec.GetSection("QueryParameters");
            if (queryParamsSec.Exists())
            {
                queryParams = [];
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
            }

            var metadataSec = routeSec.GetSection("Metadata");
            var metadata = metadataSec.GetChildren().ToDictionary(c => c.Key, c => c.Value ?? string.Empty);

            var authPolicy = routeSec["AuthorizationPolicy"];
            var corsPolicy = routeSec["CorsPolicy"];
            var rateLimiterPolicy = routeSec["RateLimiterPolicy"];
            var outputCachePolicy = routeSec["OutputCachePolicy"];
            var timeout = TimeSpan.TryParse(routeSec["Timeout"], out var parsedTimeout) ? parsedTimeout : (TimeSpan?)null;
            var maxRequestBodySize = long.TryParse(routeSec["MaxRequestBodySize"], out var parsedSize) ? parsedSize : (long?)null;
            var order = int.TryParse(routeSec["Order"], out var parsedOrder) ? parsedOrder : (int?)null;
            var requireTenant = bool.TryParse(routeSec["RequireTenant"], out var parsedRt) && parsedRt;
            var allowedContentTypes = routeSec.GetSection("AllowedContentTypes").GetChildren().Select(c => c.Value).OfType<string>().ToList();

            KyrolusIpFilterOptions? ipFilter = null;
            var ipFilterSec = routeSec.GetSection("IpFilter");
            if (ipFilterSec.Exists())
            {
                var allowed = ipFilterSec.GetSection("AllowedIpsOrCidrs").GetChildren().Select(c => c.Value).OfType<string>().ToList();
                var blocked = ipFilterSec.GetSection("BlockedIpsOrCidrs").GetChildren().Select(c => c.Value).OfType<string>().ToList();
                if (allowed.Count > 0 || blocked.Count > 0)
                {
                    ipFilter = new KyrolusIpFilterOptions
                    {
                        AllowedIpsOrCidrs = allowed.Count > 0 ? allowed : null,
                        BlockedIpsOrCidrs = blocked.Count > 0 ? blocked : null
                    };
                }
            }

            List<IReadOnlyDictionary<string, string>>? transformsList = null;
            var transformsSec = routeSec.GetSection("Transforms");
            if (transformsSec.Exists())
            {
                transformsList = [];
                foreach (var transformItem in transformsSec.GetChildren())
                {
                    var dict = transformItem.GetChildren().ToDictionary(c => c.Key, c => c.Value ?? string.Empty);
                    if (dict.Count > 0)
                        transformsList.Add(dict);
                }
            }

            _routes[routeId] = new KyrolusGatewayRoute
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new KyrolusGatewayRouteMatch
                {
                    Path = path,
                    Methods = methods.Count > 0 ? methods : null,
                    Hosts = hosts.Count > 0 ? hosts : null,
                    Headers = headers is { Count: > 0 } ? headers : null,
                    QueryParameters = queryParams is { Count: > 0 } ? queryParams : null
                },
                Metadata = metadata.Count > 0 ? metadata : null,
                AuthorizationPolicy = authPolicy,
                CorsPolicy = corsPolicy,
                RateLimiterPolicy = rateLimiterPolicy,
                OutputCachePolicy = outputCachePolicy,
                Timeout = timeout,
                MaxRequestBodySize = maxRequestBodySize,
                Order = order,
                RequireTenant = requireTenant,
                AllowedContentTypes = allowedContentTypes.Count > 0 ? allowedContentTypes : null,
                IpFilter = ipFilter,
                Transforms = transformsList
            };
            _configRouteIds.Add(routeId);
        }
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
