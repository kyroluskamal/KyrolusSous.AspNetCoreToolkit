using KyrolusSous.Gateway.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace KyrolusSous.Gateway.Yarp;

public sealed class KyrolusCorrelationTransformProvider : ITransformProvider
{
    private const string HeaderName = "X-Correlation-ID";

    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            if (!transformContext.HttpContext.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrEmpty(correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
            }

            transformContext.ProxyRequest.Headers.Remove(HeaderName);
            transformContext.ProxyRequest.Headers.Add(HeaderName, correlationId.ToString());
            return ValueTask.CompletedTask;
        });
    }
}

public sealed class KyrolusSecurityHeadersTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddResponseTransform(transformContext =>
        {
            var headers = transformContext.HttpContext.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["X-XSS-Protection"] = "1; mode=block";
            return ValueTask.CompletedTask;
        });
    }
}

public sealed class KyrolusDynamicInMemoryRouteConfigProvider : IProxyConfigProvider, IKyrolusDynamicRouteProvider
{
    private sealed class CustomProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : IProxyConfig
    {
        public IReadOnlyList<RouteConfig> Routes { get; } = routes;
        public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
        public IChangeToken ChangeToken { get; } = new CancellationChangeToken(CancellationToken.None);
    }

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

        return new CustomProxyConfig(yarpRoutes, yarpClusters);
    }

    public Task<IReadOnlyList<KyrolusGatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KyrolusGatewayRoute>>(_routes.AsReadOnly());

    public Task<IReadOnlyList<KyrolusGatewayCluster>> GetClustersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<KyrolusGatewayCluster>>(_clusters.AsReadOnly());

    public Task ReloadAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusYarpGateway(this IServiceCollection services, Action<KyrolusDynamicInMemoryRouteConfigProvider>? configure = null)
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        configure?.Invoke(provider);

        services.AddSingleton<IProxyConfigProvider>(provider);
        services.AddSingleton<IKyrolusDynamicRouteProvider>(provider);
        services.AddSingleton<ITransformProvider, KyrolusCorrelationTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusSecurityHeadersTransformProvider>();
        services.AddReverseProxy();

        return services;
    }
}
