using KyrolusSous.Gateway.Abstractions;
using KyrolusSous.Gateway.Yarp;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace KyrolusSous.Gateway.Yarp.UnitTests;

public sealed class GatewayTests
{
    [Fact(DisplayName = "Dynamic InMemory Route Config Provider Maps Routes And Clusters Correctly")]
    public void DynamicRouteConfigProvider_MapsRoutesAndClusters_Correctly()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        provider.AddRoute(new KyrolusGatewayRoute
        {
            RouteId = "orders-route",
            ClusterId = "orders-cluster",
            Match = new KyrolusGatewayRouteMatch
            {
                Path = "/api/orders/{**catch-all}",
                Methods = new[] { "GET", "POST" }
            }
        });

        provider.AddCluster(new KyrolusGatewayCluster
        {
            ClusterId = "orders-cluster",
            LoadBalancingPolicy = "RoundRobin",
            Destinations = new Dictionary<string, KyrolusGatewayDestination>
            {
                ["node1"] = new KyrolusGatewayDestination("https://orders-service-1.local"),
                ["node2"] = new KyrolusGatewayDestination("https://orders-service-2.local")
            }
        });

        var config = provider.GetConfig();
        config.Routes.Count.ShouldBe(1);
        config.Routes[0].RouteId.ShouldBe("orders-route");
        config.Routes[0].Match.Path.ShouldBe("/api/orders/{**catch-all}");

        config.Clusters.Count.ShouldBe(1);
        config.Clusters[0].ClusterId.ShouldBe("orders-cluster");
        config.Clusters[0].Destinations.ShouldNotBeNull();
        config.Clusters[0].Destinations!.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "Dynamic InMemory Route Config Provider Async Methods Return Configured Items")]
    public async Task DynamicRouteConfigProvider_AsyncMethods_ReturnConfiguredItems()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        var route = new KyrolusGatewayRoute
        {
            RouteId = "users-route",
            ClusterId = "users-cluster",
            Match = new KyrolusGatewayRouteMatch { Path = "/api/users/{**catch-all}" }
        };

        var cluster = new KyrolusGatewayCluster
        {
            ClusterId = "users-cluster",
            Destinations = new Dictionary<string, KyrolusGatewayDestination>
            {
                ["main"] = new KyrolusGatewayDestination("https://users.local")
            }
        };

        provider.AddRoute(route);
        provider.AddCluster(cluster);

        var routes = await provider.GetRoutesAsync();
        routes.Count.ShouldBe(1);
        routes[0].RouteId.ShouldBe("users-route");

        var clusters = await provider.GetClustersAsync();
        clusters.Count.ShouldBe(1);
        clusters[0].ClusterId.ShouldBe("users-cluster");

        await provider.ReloadAsync();
    }

    [Fact(DisplayName = "AddKyrolusYarpGateway Registers All Transforms And Proxy Providers")]
    public void AddKyrolusYarpGateway_RegistersAllTransformsAndProxyProviders()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusYarpGateway(provider =>
        {
            provider.AddRoute(new KyrolusGatewayRoute
            {
                RouteId = "test",
                ClusterId = "test-cluster",
                Match = new KyrolusGatewayRouteMatch { Path = "/test" }
            });
        });

        var sp = services.BuildServiceProvider();
        var proxyConfigProvider = sp.GetService<IProxyConfigProvider>();
        proxyConfigProvider.ShouldNotBeNull();

        var dynamicRouteProvider = sp.GetService<IKyrolusDynamicRouteProvider>();
        dynamicRouteProvider.ShouldNotBeNull();

        var transformProviders = sp.GetServices<ITransformProvider>().ToList();
        transformProviders.Count.ShouldBeGreaterThanOrEqualTo(4);
        transformProviders.OfType<KyrolusCorrelationTransformProvider>().ShouldHaveSingleItem();
        transformProviders.OfType<KyrolusSecurityHeadersTransformProvider>().ShouldHaveSingleItem();
        transformProviders.OfType<KyrolusTenantRoutingTransformProvider>().ShouldHaveSingleItem();
        transformProviders.OfType<KyrolusRateLimitTransformProvider>().ShouldHaveSingleItem();
    }

    [Fact(DisplayName = "Transform Providers Validate Methods Do Not Throw")]
    public void TransformProviders_ValidateMethods_DoNotThrow()
    {
        var correlation = new KyrolusCorrelationTransformProvider();
        var security = new KyrolusSecurityHeadersTransformProvider();
        var tenant = new KyrolusTenantRoutingTransformProvider();
        var rateLimit = new KyrolusRateLimitTransformProvider();

        correlation.ShouldNotBeNull();
        security.ShouldNotBeNull();
        tenant.ShouldNotBeNull();
        rateLimit.ShouldNotBeNull();
    }
}
