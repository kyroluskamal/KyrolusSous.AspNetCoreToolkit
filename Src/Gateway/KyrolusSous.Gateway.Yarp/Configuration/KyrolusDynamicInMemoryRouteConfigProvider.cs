using Yarp.ReverseProxy.Forwarder;

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
public sealed class KyrolusDynamicInMemoryRouteConfigProvider : IProxyConfigProvider, IKyrolusDynamicRouteProvider
{
    private readonly object _syncLock = new();
    private readonly Dictionary<string, KyrolusGatewayRoute> _routes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KyrolusGatewayCluster> _clusters = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource _changeTokenSource = new();
    private volatile KyrolusCustomProxyConfig _currentConfig = null!;

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
    /// Loads routes and clusters from a configuration section (e.g., from appsettings.json <c>"ReverseProxy"</c> section).
    /// </summary>
    /// <param name="section">The configuration section containing <c>Clusters</c> and <c>Routes</c> children.</param>
    /// <returns>The provider instance for fluent chaining.</returns>
    public KyrolusDynamicInMemoryRouteConfigProvider LoadFromConfiguration(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        lock (_syncLock)
        {
            var clustersSection = section.GetSection("Clusters");
            if (!clustersSection.Exists())
                throw new InvalidOperationException($"Configuration section '{section.Path}:Clusters' is missing or empty. Cannot load clusters without a valid configuration.");
            LoadCluster(clustersSection.GetChildren());

            var routesSection = section.GetSection("Routes");
            if (!routesSection.Exists())
                LoadRoutes(section.GetChildren().Where(s => s.Key != "Clusters"));
            else
                LoadRoutes(routesSection.GetChildren());

            SignalChange();
        }

        return this;
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
        var yarpRoutes = _routes.Values.Select(r => new RouteConfig
        {
            RouteId = r.RouteId,
            ClusterId = r.ClusterId,
            Match = new RouteMatch
            {
                Path = r.Match.Path,
                Methods = r.Match.Methods,
                Hosts = r.Match.Hosts
            },
            Metadata = r.Metadata,
            AuthorizationPolicy = r.AuthorizationPolicy,
            CorsPolicy = r.CorsPolicy,
            RateLimiterPolicy = r.RateLimiterPolicy,
            Timeout = r.Timeout,
            Transforms = r.Transforms
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
                    Path = c.HealthCheck.Active.Path
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
                AffinityKeyName = c.SessionAffinity.AffinityKeyName ?? "KyrolusAffinity"
            },
            HttpRequest = c.HttpRequestTimeout.HasValue ? new ForwarderRequestConfig
            {
                ActivityTimeout = c.HttpRequestTimeout.Value
            } : null
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
                sessionAffinity = new KyrolusSessionAffinityOptions
                {
                    Enabled = bool.TryParse(affinitySec["Enabled"], out var aen) && aen,
                    Policy = affinitySec["Policy"],
                    FailurePolicy = affinitySec["FailurePolicy"],
                    AffinityKeyName = affinitySec["AffinityKeyName"]
                };
            }

            TimeSpan? httpTimeout = null;
            var httpRequestSec = clusterSec.GetSection("HttpRequest");
            if (httpRequestSec.Exists() && TimeSpan.TryParse(httpRequestSec["Timeout"], out var toVal))
            {
                httpTimeout = toVal;
            }
            else if (TimeSpan.TryParse(clusterSec["HttpRequestTimeout"], out var directTo))
            {
                httpTimeout = directTo;
            }

            _clusters[clusterId] = new KyrolusGatewayCluster
            {
                ClusterId = clusterId,
                Destinations = destinations,
                LoadBalancingPolicy = loadBalancingPolicy,
                HealthCheck = healthCheck,
                SessionAffinity = sessionAffinity,
                HttpRequestTimeout = httpTimeout
            };
        }
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

            var metadataSec = routeSec.GetSection("Metadata");
            var metadata = metadataSec.GetChildren().ToDictionary(c => c.Key, c => c.Value ?? string.Empty);

            var authPolicy = routeSec["AuthorizationPolicy"];
            var corsPolicy = routeSec["CorsPolicy"];
            var rateLimiterPolicy = routeSec["RateLimiterPolicy"];
            var timeout = TimeSpan.TryParse(routeSec["Timeout"], out var parsedTimeout) ? parsedTimeout : (TimeSpan?)null;

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
                    Hosts = hosts.Count > 0 ? hosts : null
                },
                Metadata = metadata.Count > 0 ? metadata : null,
                AuthorizationPolicy = authPolicy,
                CorsPolicy = corsPolicy,
                RateLimiterPolicy = rateLimiterPolicy,
                Timeout = timeout,
                Transforms = transformsList
            };
        }
    }
}
