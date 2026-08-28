using KyrolusSous.Gateway.Abstractions;
using KyrolusSous.Gateway.Yarp;
using Shouldly;
using Xunit;

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
}
