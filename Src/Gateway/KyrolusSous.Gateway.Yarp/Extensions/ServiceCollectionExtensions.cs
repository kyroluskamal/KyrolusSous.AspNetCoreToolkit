using KyrolusSous.Gateway.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Extension methods for registering Kyrolus YARP Gateway services in the dependency injection container.
/// </summary>
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
        services.AddSingleton<ITransformProvider, KyrolusTenantRoutingTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusRateLimitTransformProvider>();
        services.AddReverseProxy();

        return services;
    }
}
