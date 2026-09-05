using KyrolusSous.Gateway.Abstractions;
using KyrolusSous.Gateway.Yarp;
using Microsoft.Extensions.Configuration;
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
            LoadBalancingPolicy = KyrolusLoadBalancingPolicies.RoundRobin,
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

    [Fact(DisplayName = "AddCluster With FluentBuilder Eliminates Repetition And Sets Child Routes")]
    public void AddCluster_WithFluentBuilder_EliminatesRepetition_AndSetsChildRoutes()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        // Write cluster name ONCE and define all routes inside it without repeating ClusterId!
        provider.AddCluster("invoices-cluster", cluster =>
        {
            cluster.WithLoadBalancing(KyrolusLoadBalancingPolicy.RoundRobin)
                   .AddDestination("srv1", "http://192.168.1.50:5000")
                   .AddDestination("srv2", "http://192.168.1.51:5000")
                   .AddRoute("invoices-all", "/api/invoices/{**catch-all}")
                   .AddRoute("invoices-reports", "/api/invoices/reports", "GET")
                   .AddRoute("invoices-create", "/api/invoices/new", "POST");
        });

        var config = provider.GetConfig();

        config.Clusters.Count.ShouldBe(1);
        var cluster = config.Clusters[0];
        cluster.ClusterId.ShouldBe("invoices-cluster");
        cluster.LoadBalancingPolicy.ShouldBe("RoundRobin");
        cluster.Destinations.ShouldNotBeNull();
        cluster.Destinations!.Count.ShouldBe(2);

        config.Routes.Count.ShouldBe(3);
        foreach (var route in config.Routes)
        {
            // All routes must automatically be linked to the cluster without repeating ClusterId!
            route.ClusterId.ShouldBe("invoices-cluster");
        }

        config.Routes[0].RouteId.ShouldBe("invoices-all");
        config.Routes[1].RouteId.ShouldBe("invoices-reports");
        config.Routes[1].Match.Methods!.ShouldContain("GET");
        config.Routes[2].RouteId.ShouldBe("invoices-create");
        config.Routes[2].Match.Methods!.ShouldContain("POST");
    }

    [Theory(DisplayName = "WithLoadBalancing Accepts Both Enum And Constants Properly")]
    [InlineData(KyrolusLoadBalancingPolicy.LeastRequests, "LeastRequests")]
    [InlineData(KyrolusLoadBalancingPolicy.Random, "Random")]
    [InlineData(KyrolusLoadBalancingPolicy.PowerOfTwoChoices, "PowerOfTwoChoices")]
    public void WithLoadBalancing_AcceptsEnumAndConstants(KyrolusLoadBalancingPolicy policy, string expectedName)
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        provider.AddCluster("test-cluster", c =>
        {
            c.WithLoadBalancing(policy)
             .AddDestination("srv", "https://localhost:5000");
        });

        var config = provider.GetConfig();
        config.Clusters[0].LoadBalancingPolicy.ShouldBe(expectedName);
    }

    [Fact(DisplayName = "LoadFromConfiguration Reads AppSettings Json Formatted Structure Successfully")]
    public void LoadFromConfiguration_ReadsAppSettingsJsonStructure()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:billing-cluster:LoadBalancingPolicy"] = "RoundRobin",
            ["ReverseProxy:Clusters:billing-cluster:Destinations:billing1:Address"] = "https://billing1.internal",
            ["ReverseProxy:Clusters:billing-cluster:Destinations:billing2:Address"] = "https://billing2.internal",

            ["ReverseProxy:Routes:billing-route:ClusterId"] = "billing-cluster",
            ["ReverseProxy:Routes:billing-route:Match:Path"] = "/api/billing/{**catch-all}",
            ["ReverseProxy:Routes:billing-route:Match:Methods:0"] = "GET",
            ["ReverseProxy:Routes:billing-route:Match:Methods:1"] = "POST"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.LoadFromConfiguration(config.GetSection("ReverseProxy"));

        var proxyConfig = provider.GetConfig();
        proxyConfig.Clusters.Count.ShouldBe(1);
        proxyConfig.Clusters[0].ClusterId.ShouldBe("billing-cluster");
        proxyConfig.Clusters[0].Destinations.ShouldNotBeNull();
        proxyConfig.Clusters[0].Destinations!.Count.ShouldBe(2);

        proxyConfig.Routes.Count.ShouldBe(1);
        proxyConfig.Routes[0].RouteId.ShouldBe("billing-route");
        proxyConfig.Routes[0].ClusterId.ShouldBe("billing-cluster");
        proxyConfig.Routes[0].Match.Path.ShouldBe("/api/billing/{**catch-all}");
        proxyConfig.Routes[0].Match.Methods!.ShouldContain("GET");
        proxyConfig.Routes[0].Match.Methods!.ShouldContain("POST");
    }

    [Fact(DisplayName = "AddKyrolusYarpGateway Supports Hybrid Mode With Configuration And Programmatic Code")]
    public void AddKyrolusYarpGateway_SupportsHybridMode()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:json-cluster:Destinations:node:Address"] = "https://json-service",
            ["ReverseProxy:Routes:json-route:ClusterId"] = "json-cluster",
            ["ReverseProxy:Routes:json-route:Match:Path"] = "/from-json"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Hybrid mode: Loads from JSON + adds extra fluent cluster in code!
        services.AddKyrolusYarpGateway(config, "ReverseProxy", gateway =>
        {
            gateway.AddCluster("code-cluster", c =>
            {
                c.WithLoadBalancing(KyrolusLoadBalancingPolicy.Random)
                 .AddDestination("node", "https://code-service")
                 .AddRoute("code-route", "/from-code");
            });
        });

        var sp = services.BuildServiceProvider();
        var proxyConfigProvider = sp.GetRequiredService<IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        // Both JSON and Code routes and clusters must be present!
        proxyConfig.Clusters.Count.ShouldBe(2);
        proxyConfig.Clusters.ShouldContain(c => c.ClusterId == "json-cluster");
        proxyConfig.Clusters.ShouldContain(c => c.ClusterId == "code-cluster");

        proxyConfig.Routes.Count.ShouldBe(2);
        proxyConfig.Routes.ShouldContain(r => r.RouteId == "json-route");
        proxyConfig.Routes.ShouldContain(r => r.RouteId == "code-route");
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
