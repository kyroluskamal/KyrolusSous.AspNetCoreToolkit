using KyrolusSous.Gateway.Abstractions;
using KyrolusSous.Gateway.Yarp.Configuration;
using KyrolusSous.Gateway.Yarp.Extensions;
using KyrolusSous.Gateway.Yarp.Transforms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
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
                Methods = new[] { KyrolusGatewayHttpMethods.Get, KyrolusGatewayHttpMethods.Post }
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
            cluster.WithLoadBalancing(KyrolusLoadBalancingPolicies.RoundRobin)
                   .AddDestination("srv1", "http://192.168.1.50:5000")
                   .AddDestination("srv2", "http://192.168.1.51:5000")
                   .AddRoute("invoices-all", "/api/invoices/{**catch-all}")
                   .AddRoute("invoices-reports", "/api/invoices/reports", KyrolusGatewayHttpMethods.Get)
                   .AddRoute("invoices-create", "/api/invoices/new", KyrolusGatewayHttpMethods.Post);
        });

        var config = provider.GetConfig();

        config.Clusters.Count.ShouldBe(1);
        var cluster = config.Clusters[0];
        cluster.ClusterId.ShouldBe("invoices-cluster");
        cluster.LoadBalancingPolicy.ShouldBe(KyrolusLoadBalancingPolicies.RoundRobin);
        cluster.Destinations.ShouldNotBeNull();
        cluster.Destinations.Count.ShouldBe(2);

        config.Routes.Count.ShouldBe(3);
        foreach (var route in config.Routes)
        {
            // All routes must automatically be linked to the cluster without repeating ClusterId!
            route.ClusterId.ShouldBe("invoices-cluster");
        }

        config.Routes[0].RouteId.ShouldBe("invoices-all");
        config.Routes[1].RouteId.ShouldBe("invoices-reports");
        config.Routes[1].Match.Methods!.ShouldContain(KyrolusGatewayHttpMethods.Get);
        config.Routes[2].RouteId.ShouldBe("invoices-create");
        config.Routes[2].Match.Methods!.ShouldContain(KyrolusGatewayHttpMethods.Post);
    }

    [Theory(DisplayName = "WithLoadBalancing Accepts Constants Properly")]
    [InlineData(KyrolusLoadBalancingPolicies.RoundRobin, "RoundRobin")]
    [InlineData(KyrolusLoadBalancingPolicies.LeastRequests, "LeastRequests")]
    [InlineData(KyrolusLoadBalancingPolicies.Random, "Random")]
    [InlineData(KyrolusLoadBalancingPolicies.PowerOfTwoChoices, "PowerOfTwoChoices")]
    [InlineData("CustomHashRingPolicy", "CustomHashRingPolicy")]
    public void WithLoadBalancing_AcceptsPolicyConstantsProperly(string policy, string expectedName)
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
                c.WithLoadBalancing(KyrolusLoadBalancingPolicies.Random)
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
        transformProviders.OfType<KyrolusTelemetryHeadersTransformProvider>().ShouldHaveSingleItem();
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

    [Fact(DisplayName = "KyrolusGatewayHttpMethods Exposes Standard Verbs Correctly")]
    public void KyrolusGatewayHttpMethods_ExposesStandardVerbs_Correctly()
    {
        KyrolusGatewayHttpMethods.Get.ShouldBe("GET");
        KyrolusGatewayHttpMethods.Post.ShouldBe("POST");
        KyrolusGatewayHttpMethods.Put.ShouldBe("PUT");
        KyrolusGatewayHttpMethods.Delete.ShouldBe("DELETE");
        KyrolusGatewayHttpMethods.Patch.ShouldBe("PATCH");
        KyrolusGatewayHttpMethods.Head.ShouldBe("HEAD");
        KyrolusGatewayHttpMethods.Options.ShouldBe("OPTIONS");
        KyrolusGatewayHttpMethods.Trace.ShouldBe("TRACE");
        KyrolusGatewayHttpMethods.Connect.ShouldBe("CONNECT");
    }

    [Fact(DisplayName = "Tenant Routing Transform Prioritizes Explicit Header Over Subdomain")]
    public async Task TenantRoutingTransform_PrioritizesHeader_OverSubdomain()
    {
        var provider = new KyrolusTenantRoutingTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        builderContext.RequestTransforms.Count.ShouldBe(1);
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("client.example.com");
        httpContext.Request.Headers["X-Tenant-ID"] = "explicit-tenant";

        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await transform.ApplyAsync(transformContext);
        proxyRequest.Headers.GetValues("X-Tenant-ID").ShouldContain("explicit-tenant");
    }

    [Theory(DisplayName = "Tenant Routing Transform Resolves Subdomain Or Ignores Reserved Names")]
    [InlineData("tenant-alpha.example.com", "tenant-alpha")]
    [InlineData("api.example.com", null)]
    [InlineData("www.example.com", null)]
    [InlineData("192.168.1.10", null)]
    [InlineData("localhost", null)]
    public async Task TenantRoutingTransform_ResolvesSubdomain_OrIgnoresReserved(string host, string? expectedTenant)
    {
        var provider = new KyrolusTenantRoutingTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);

        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await transform.ApplyAsync(transformContext);

        if (expectedTenant is not null)
        {
            proxyRequest.Headers.GetValues("X-Tenant-ID").ShouldContain(expectedTenant);
        }
        else
        {
            proxyRequest.Headers.Contains("X-Tenant-ID").ShouldBeFalse();
        }
    }

    [Fact(DisplayName = "Dynamic Reloading Triggers ChangeToken Callback Successfully")]
    public void DynamicReloading_TriggersChangeTokenCallback_Successfully()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        var initialConfig = provider.GetConfig();
        var token = initialConfig.ChangeToken;

        var callbackFired = false;
        token.RegisterChangeCallback(_ => callbackFired = true, null);

        token.HasChanged.ShouldBeFalse();
        callbackFired.ShouldBeFalse();

        // Mutate configuration by adding a cluster
        provider.AddCluster("orders", c =>
        {
            c.AddDestination("node1", "http://orders:5000")
             .AddRoute("orders-r", "/orders");
        });

        token.HasChanged.ShouldBeTrue();
        callbackFired.ShouldBeTrue();

        var updatedConfig = provider.GetConfig();
        updatedConfig.Clusters.Count.ShouldBe(1);
        updatedConfig.Routes.Count.ShouldBe(1);
        updatedConfig.ChangeToken.HasChanged.ShouldBeFalse();
    }

    [Fact(DisplayName = "Provider Supports Concurrent Reads And Writes Without Corruption")]
    public void Provider_SupportsConcurrentReadsAndWrites()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        Parallel.For(0, 100, i =>
        {
            provider.AddRoute(new KyrolusGatewayRoute
            {
                RouteId = $"route-{i}",
                ClusterId = $"cluster-{i % 10}",
                Match = new KyrolusGatewayRouteMatch { Path = $"/path/{i}" }
            });

            provider.AddCluster(new KyrolusGatewayCluster
            {
                ClusterId = $"cluster-{i % 10}",
                Destinations = new Dictionary<string, KyrolusGatewayDestination>
                {
                    ["node"] = new KyrolusGatewayDestination($"http://host-{i % 10}:5000")
                }
            });

            var config = provider.GetConfig();
            config.ShouldNotBeNull();
        });

        var finalConfig = provider.GetConfig();
        finalConfig.Routes.Count.ShouldBe(100);
        finalConfig.Clusters.Count.ShouldBe(10);
    }

    [Fact(DisplayName = "Deduplication Updates Existing Routes And Clusters Instead Of Duplicating")]
    public void Deduplication_UpdatesExistingRoutesAndClusters()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        provider.AddRoute(new KyrolusGatewayRoute
        {
            RouteId = "orders-route",
            ClusterId = "orders-v1",
            Match = new KyrolusGatewayRouteMatch { Path = "/orders/v1" }
        });

        // Upsert same RouteId with new destination and path
        provider.AddRoute(new KyrolusGatewayRoute
        {
            RouteId = "orders-route",
            ClusterId = "orders-v2",
            Match = new KyrolusGatewayRouteMatch { Path = "/orders/v2" }
        });

        var config = provider.GetConfig();
        config.Routes.Count.ShouldBe(1);
        config.Routes[0].ClusterId.ShouldBe("orders-v2");
        config.Routes[0].Match.Path.ShouldBe("/orders/v2");
    }

    [Fact(DisplayName = "Correlation Transform Sanitizes Malicious Header And Echoes In Response")]
    public async Task CorrelationTransform_SanitizesMaliciousHeader_AndEchoesInResponse()
    {
        var provider = new KyrolusCorrelationTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        builderContext.RequestTransforms.Count.ShouldBe(1);
        builderContext.ResponseTransforms.Count.ShouldBe(1);

        var httpContext = new DefaultHttpContext();
        // Attack: CRLF injection attempt
        httpContext.Request.Headers["X-Correlation-ID"] = "malicious\r\nInjected-Header: evil";

        var proxyRequest = new HttpRequestMessage();
        var requestTransformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await builderContext.RequestTransforms[0].ApplyAsync(requestTransformContext);

        var forwardedId = proxyRequest.Headers.GetValues("X-Correlation-ID").Single();
        // Header injection must be rejected and replaced with safe GUID
        forwardedId.ShouldNotContain("\r");
        forwardedId.ShouldNotContain("\n");
        forwardedId.Length.ShouldBe(32);

        // Response Echo test
        var responseTransformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        await builderContext.ResponseTransforms[0].ApplyAsync(responseTransformContext);
        httpContext.Response.Headers["X-Correlation-ID"].ToString().ShouldBe(forwardedId);
    }

    [Fact(DisplayName = "Correlation Transform Preserves Valid Correlation ID And Echoes In Response")]
    public async Task CorrelationTransform_PreservesValidHeader_AndEchoesInResponse()
    {
        var provider = new KyrolusCorrelationTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        var httpContext = new DefaultHttpContext();
        const string validId = "custom-trace-id-12345_XYZ";
        httpContext.Request.Headers["X-Correlation-ID"] = validId;

        var proxyRequest = new HttpRequestMessage();
        var requestTransformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await builderContext.RequestTransforms[0].ApplyAsync(requestTransformContext);
        proxyRequest.Headers.GetValues("X-Correlation-ID").Single().ShouldBe(validId);

        var responseTransformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        await builderContext.ResponseTransforms[0].ApplyAsync(responseTransformContext);
        httpContext.Response.Headers["X-Correlation-ID"].ToString().ShouldBe(validId);
    }

    [Fact(DisplayName = "Security Headers Transform Injects Modern Defensive Headers")]
    public async Task SecurityHeadersTransform_InjectsModernDefensiveHeaders()
    {
        var provider = new KyrolusSecurityHeadersTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        var httpContext = new DefaultHttpContext();
        var responseTransformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        await builderContext.ResponseTransforms[0].ApplyAsync(responseTransformContext);

        var headers = httpContext.Response.Headers;
        headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        headers["X-Frame-Options"].ToString().ShouldBe("DENY");
        headers["Referrer-Policy"].ToString().ShouldBe("strict-origin-when-cross-origin");
        headers["Permissions-Policy"].ToString().ShouldContain("camera=()");
        headers["X-XSS-Protection"].ToString().ShouldBe("0");
    }

    [Fact(DisplayName = "Fluent RouteBuilder Configures Policies Timeouts And PathTransforms")]
    public void FluentRouteBuilder_ConfiguresPoliciesTimeoutsAndPathTransforms()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        provider.AddCluster("catalog-cluster", cluster =>
        {
            cluster.AddDestination("node1", "http://localhost:5001")
                   .AddRoute("catalog-query", "/api/catalog/{**catch-all}", route =>
                   {
                       route.WithMethods(KyrolusGatewayHttpMethods.Get)
                            .WithAuthorization("RequireAdminRole")
                            .WithCors("AllowAngularClient")
                            .WithRateLimiter("StrictCatalogLimiter")
                            .WithTimeout(TimeSpan.FromSeconds(15))
                            .WithTransformPathRemovePrefix("/api")
                            .WithMetadata("Tier", "Gold");
                   });
        });

        var config = provider.GetConfig();
        var route = config.Routes.Single(r => r.RouteId == "catalog-query");

        route.ClusterId.ShouldBe("catalog-cluster");
        route.AuthorizationPolicy.ShouldBe("RequireAdminRole");
        route.CorsPolicy.ShouldBe("AllowAngularClient");
        route.RateLimiterPolicy.ShouldBe("StrictCatalogLimiter");
        route.Timeout.ShouldBe(TimeSpan.FromSeconds(15));
        route.Metadata.ShouldNotBeNull();
        route.Metadata["Tier"].ShouldBe("Gold");
        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(1);
        route.Transforms[0]["PathRemovePrefix"].ShouldBe("/api");
    }

    [Fact(DisplayName = "Fluent ClusterBuilder Configures HealthCheck SessionAffinity And Timeout")]
    public void FluentClusterBuilder_ConfiguresHealthCheckSessionAffinityAndTimeout()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        provider.AddCluster("payments-cluster", cluster =>
        {
            cluster.WithLoadBalancing(KyrolusLoadBalancingPolicies.LeastRequests)
                   .WithTimeout(TimeSpan.FromSeconds(45))
                   .WithHealthCheck(new KyrolusHealthCheckOptions
                   {
                       Active = new KyrolusActiveHealthCheckOptions
                       {
                           Enabled = true,
                           Path = "/healthz",
                           Interval = TimeSpan.FromSeconds(10),
                           Timeout = TimeSpan.FromSeconds(3)
                       },
                       Passive = new KyrolusPassiveHealthCheckOptions
                       {
                           Enabled = true,
                           ReactivationPeriod = TimeSpan.FromMinutes(2)
                       }
                   })
                   .WithSessionAffinity(new KyrolusSessionAffinityOptions
                   {
                       Enabled = true,
                       Policy = "Cookie",
                       FailurePolicy = "Redistribute",
                       AffinityKeyName = "PaymentSessionCookie"
                   })
                   .AddDestination("pay1", "https://pay1.internal")
                   .AddRoute("pay-route", "/pay");
        });

        var config = provider.GetConfig();
        var cluster = config.Clusters.Single(c => c.ClusterId == "payments-cluster");

        cluster.LoadBalancingPolicy.ShouldBe(KyrolusLoadBalancingPolicies.LeastRequests);
        cluster.HttpRequest.ShouldNotBeNull();
        cluster.HttpRequest.ActivityTimeout.ShouldBe(TimeSpan.FromSeconds(45));

        cluster.HealthCheck.ShouldNotBeNull();
        cluster.HealthCheck.Active.ShouldNotBeNull();
        cluster.HealthCheck.Active.Enabled.ShouldBe(true);
        cluster.HealthCheck.Active.Path.ShouldBe("/healthz");
        cluster.HealthCheck.Active.Interval.ShouldBe(TimeSpan.FromSeconds(10));
        cluster.HealthCheck.Active.Timeout.ShouldBe(TimeSpan.FromSeconds(3));

        cluster.HealthCheck.Passive.ShouldNotBeNull();
        cluster.HealthCheck.Passive.Enabled.ShouldBe(true);
        cluster.HealthCheck.Passive.ReactivationPeriod.ShouldBe(TimeSpan.FromMinutes(2));

        cluster.SessionAffinity.ShouldNotBeNull();
        cluster.SessionAffinity.Enabled.ShouldBe(true);
        cluster.SessionAffinity.Policy.ShouldBe("Cookie");
        cluster.SessionAffinity.FailurePolicy.ShouldBe("Redistribute");
        cluster.SessionAffinity.AffinityKeyName.ShouldBe("PaymentSessionCookie");
    }

    [Fact(DisplayName = "LoadFromConfiguration Reads Full Enterprise Clusters And Routes Successfully")]
    public void LoadFromConfiguration_ReadsFullEnterpriseClustersAndRoutes()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:auth-cluster:LoadBalancingPolicy"] = "RoundRobin",
            ["ReverseProxy:Clusters:auth-cluster:Destinations:auth1:Address"] = "https://auth1.internal",
            ["ReverseProxy:Clusters:auth-cluster:HttpRequest:Timeout"] = "00:00:20",
            ["ReverseProxy:Clusters:auth-cluster:HealthCheck:Active:Enabled"] = "true",
            ["ReverseProxy:Clusters:auth-cluster:HealthCheck:Active:Path"] = "/health",
            ["ReverseProxy:Clusters:auth-cluster:HealthCheck:Active:Interval"] = "00:00:15",
            ["ReverseProxy:Clusters:auth-cluster:SessionAffinity:Enabled"] = "true",
            ["ReverseProxy:Clusters:auth-cluster:SessionAffinity:Policy"] = "Cookie",
            ["ReverseProxy:Clusters:auth-cluster:SessionAffinity:AffinityKeyName"] = "AuthAffinity",

            ["ReverseProxy:Routes:auth-route:ClusterId"] = "auth-cluster",
            ["ReverseProxy:Routes:auth-route:Match:Path"] = "/api/auth/{**catch-all}",
            ["ReverseProxy:Routes:auth-route:AuthorizationPolicy"] = "RequireAuth",
            ["ReverseProxy:Routes:auth-route:CorsPolicy"] = "AllowClient",
            ["ReverseProxy:Routes:auth-route:RateLimiterPolicy"] = "AuthRateLimit",
            ["ReverseProxy:Routes:auth-route:Timeout"] = "00:00:10",
            ["ReverseProxy:Routes:auth-route:Transforms:0:PathRemovePrefix"] = "/api"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.LoadFromConfiguration(config.GetSection("ReverseProxy"));

        var proxyConfig = provider.GetConfig();
        var cluster = proxyConfig.Clusters.Single(c => c.ClusterId == "auth-cluster");
        cluster.HttpRequest.ShouldNotBeNull();
        cluster.HttpRequest.ActivityTimeout.ShouldBe(TimeSpan.FromSeconds(20));
        cluster.HealthCheck.ShouldNotBeNull();
        cluster.HealthCheck.Active!.Enabled.ShouldBe(true);
        cluster.HealthCheck.Active.Path.ShouldBe("/health");
        cluster.SessionAffinity.ShouldNotBeNull();
        cluster.SessionAffinity.Enabled.ShouldBe(true);
        cluster.SessionAffinity.AffinityKeyName.ShouldBe("AuthAffinity");

        var route = proxyConfig.Routes.Single(r => r.RouteId == "auth-route");
        route.AuthorizationPolicy.ShouldBe("RequireAuth");
        route.CorsPolicy.ShouldBe("AllowClient");
        route.RateLimiterPolicy.ShouldBe("AuthRateLimit");
        route.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(1);
        route.Transforms[0]["PathRemovePrefix"].ShouldBe("/api");
    }
}
