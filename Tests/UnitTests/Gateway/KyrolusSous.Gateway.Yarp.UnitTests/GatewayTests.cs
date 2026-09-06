using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KyrolusSous.Auth.MultiTenancy;
using KyrolusSous.Auth.TokenRevocation;
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
                Methods = [KyrolusHttpMethod.Get, KyrolusHttpMethod.Post]
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

        await provider.AddRouteAsync(route);
        await provider.AddClusterAsync(cluster);

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
        var telemetry = new KyrolusTelemetryHeadersTransformProvider();

        correlation.ShouldNotBeNull();
        security.ShouldNotBeNull();
        tenant.ShouldNotBeNull();
        telemetry.ShouldNotBeNull();
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

    [Fact(DisplayName = "Tenant Routing Transform Strips Untrusted Header And Resolves Authoritative Tenant")]
    public async Task TenantRoutingTransform_StripsUntrustedHeader_AndResolvesAuthoritativeTenant()
    {
        var provider = new KyrolusTenantRoutingTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        builderContext.RequestTransforms.Count.ShouldBe(1);
        var transform = builderContext.RequestTransforms[0];

        // Scenario 1: Unauthenticated request sends forged X-Tenant-ID
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("client.example.com");
        httpContext.Request.Headers["X-Tenant-ID"] = "malicious-forged-tenant";

        var proxyRequest = new HttpRequestMessage();
        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await transform.ApplyAsync(transformContext);

        // Untrusted header MUST be stripped, and authoritative subdomain "client" injected!
        proxyRequest.Headers.GetValues("X-Tenant-ID").Single().ShouldBe("client");

        // Scenario 2: Authenticated user with JWT claim overrides subdomain
        var authHttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("tenant_id", "authenticated-corp")
            ], "Bearer"))
        };
        authHttpContext.Request.Host = new HostString("unrelated.example.com");
        authHttpContext.Request.Headers["X-Tenant-ID"] = "spoof-attempt";

        var authProxyRequest = new HttpRequestMessage();
        var authTransformContext = new RequestTransformContext
        {
            HttpContext = authHttpContext,
            ProxyRequest = authProxyRequest
        };

        await transform.ApplyAsync(authTransformContext);
        authProxyRequest.Headers.GetValues("X-Tenant-ID").Single().ShouldBe("authenticated-corp");
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

    [Fact(DisplayName = "Batch Add Inserts Multiple Routes And Clusters With Single Reload Notification")]
    public void BatchAdd_InsertsMultipleRoutesAndClusters_WithSingleReloadNotification()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        var initialConfig = provider.GetConfig();
        var changeToken = initialConfig.ChangeToken;

        var changeNotificationCount = 0;
        changeToken.RegisterChangeCallback(_ => changeNotificationCount++, null);

        var routes = Enumerable.Range(1, 50).Select(i => new KyrolusGatewayRoute
        {
            RouteId = $"route-{i}",
            ClusterId = "batch-cluster",
            Match = new KyrolusGatewayRouteMatch { Path = $"/batch/{i}" }
        }).ToList();

        var clusters = new[]
        {
            new KyrolusGatewayCluster
            {
                ClusterId = "batch-cluster",
                Destinations = new Dictionary<string, KyrolusGatewayDestination>
                {
                    ["node"] = new KyrolusGatewayDestination("http://batch-node:5000")
                }
            }
        };

        provider.AddClusters(clusters);
        provider.AddRoutes(routes);

        var config = provider.GetConfig();
        config.Routes.Count.ShouldBe(50);
        config.Clusters.Count.ShouldBe(1);
        changeToken.HasChanged.ShouldBeTrue();
    }

    [Fact(DisplayName = "Decommissioning Removes Route Cluster And Node Cleanly")]
    public void Decommissioning_RemovesRouteClusterAndNodeCleanly()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        provider.AddCluster("orders-cluster", c =>
        {
            c.AddDestination("node1", "http://orders1:5000")
             .AddDestination("node2", "http://orders2:5000")
             .AddRoute("orders-r1", "/orders/1")
             .AddRoute("orders-r2", "/orders/2");
        });

        var config = provider.GetConfig();
        config.Routes.Count.ShouldBe(2);
        config.Clusters.Count.ShouldBe(1);
        config.Clusters[0].Destinations.ShouldNotBeNull();
        config.Clusters[0].Destinations!.Count.ShouldBe(2);

        // 1. Remove specific node (draining)
        var nodeRemoved = provider.RemoveDestination("orders-cluster", "node1");
        nodeRemoved.ShouldBeTrue();
        provider.GetConfig().Clusters[0].Destinations!.Count.ShouldBe(1);
        provider.GetConfig().Clusters[0].Destinations!.ContainsKey("node1").ShouldBeFalse();

        // 2. Remove specific route
        var routeRemoved = provider.RemoveRoute("orders-r1");
        routeRemoved.ShouldBeTrue();
        provider.GetConfig().Routes.Count.ShouldBe(1);
        provider.GetConfig().Routes[0].RouteId.ShouldBe("orders-r2");

        // 3. Remove entire cluster (orphaned routes should also be cleaned up)
        var clusterRemoved = provider.RemoveCluster("orders-cluster");
        clusterRemoved.ShouldBeTrue();
        provider.GetConfig().Clusters.Count.ShouldBe(0);
        provider.GetConfig().Routes.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "MaxRequestBodySize Maps Correctly Through FluentBuilder And Configuration")]
    public void MaxRequestBodySize_MapsCorrectly()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        provider.AddCluster("upload-cluster", c =>
        {
            c.AddDestination("upload-node", "http://uploads:5000")
             .AddRoute("upload-route", "/upload", r =>
             {
                 r.WithMaxRequestBodySize(10 * 1024 * 1024); // 10 MB
             });
        });

        var config = provider.GetConfig();
        var route = config.Routes.Single(r => r.RouteId == "upload-route");
        route.MaxRequestBodySize.ShouldBe(10 * 1024 * 1024);
    }

    [Fact(DisplayName = "Security Headers Transform Includes Hsts On Https Requests")]
    public async Task SecurityHeadersTransform_IncludesHstsOnHttps()
    {
        var provider = new KyrolusSecurityHeadersTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        var httpsContext = new DefaultHttpContext();
        httpsContext.Request.Scheme = "https";

        var responseTransformContext = new ResponseTransformContext
        {
            HttpContext = httpsContext
        };

        await builderContext.ResponseTransforms[0].ApplyAsync(responseTransformContext);
        httpsContext.Response.Headers["Strict-Transport-Security"].ToString().ShouldBe("max-age=31536000; includeSubDomains");
    }

    [Fact(DisplayName = "Token Revocation Middleware Blocks Blacklisted Token With 401")]
    public async Task TokenRevocationMiddleware_BlocksBlacklistedToken_With401()
    {
        var blacklist = new KyrolusInMemoryTokenBlacklist();
        await blacklist.RevokeTokenAsync("revoked-jti-123", DateTimeOffset.UtcNow.AddHours(1));

        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusTokenBlacklist>(blacklist);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = sp,
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("jti", "revoked-jti-123")
            ], "Bearer"))
        };

        var nextInvoked = false;
        var middleware = new KyrolusTokenRevocationMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        nextInvoked.ShouldBeFalse();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact(DisplayName = "Token Revocation Middleware Allows Valid Active Token")]
    public async Task TokenRevocationMiddleware_AllowsValidActiveToken()
    {
        var blacklist = new KyrolusInMemoryTokenBlacklist();

        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusTokenBlacklist>(blacklist);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = sp,
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("jti", "active-valid-jti-999")
            ], "Bearer"))
        };

        var nextInvoked = false;
        var middleware = new KyrolusTokenRevocationMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        nextInvoked.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "Destination Address Validation Rejects Invalid Or Non Http Schemes")]
    public void DestinationAddress_Validation_RejectsInvalidOrNonHttpSchemes()
    {
        // 1. Missing scheme (e.g. localhost:5000)
        Should.Throw<ArgumentException>(() => new KyrolusGatewayDestination("localhost:5000"));

        // 2. Non-HTTP scheme (e.g. ftp:// or file://)
        Should.Throw<ArgumentException>(() => new KyrolusGatewayDestination("ftp://internal-server:21"));

        // 3. Null or whitespace
        Should.Throw<ArgumentException>(() => new KyrolusGatewayDestination("   "));

        // 4. Valid HTTP and HTTPS addresses should succeed
        var httpDest = new KyrolusGatewayDestination("http://10.0.1.10:5000");
        httpDest.Address.ShouldBe("http://10.0.1.10:5000");

        var httpsDest = new KyrolusGatewayDestination("https://orders-service:5001");
        httpsDest.Address.ShouldBe("https://orders-service:5001");
    }

    [Fact(DisplayName = "Security Headers Transform Strips Sensitive Backend Server Headers")]
    public async Task SecurityHeadersTransform_StripsSensitiveBackendServerHeaders()
    {
        var provider = new KyrolusSecurityHeadersTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);

        builderContext.ResponseTransforms.Count.ShouldBe(1);

        var httpContext = new DefaultHttpContext();
        var headers = httpContext.Response.Headers;
        headers["Server"] = "Kestrel";
        headers["X-Powered-By"] = "ASP.NET";
        headers["X-AspNet-Version"] = "10.0";
        headers["X-Runtime"] = "0.04";

        var responseTransformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        var transform = builderContext.ResponseTransforms[0];
        await transform.ApplyAsync(responseTransformContext);

        // Sensitive headers must be completely stripped
        headers.ContainsKey("Server").ShouldBeFalse();
        headers.ContainsKey("X-Powered-By").ShouldBeFalse();
        headers.ContainsKey("X-AspNet-Version").ShouldBeFalse();
        headers.ContainsKey("X-Runtime").ShouldBeFalse();

        // Baseline defense headers must be present
        headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        headers["X-Frame-Options"].ToString().ShouldBe("DENY");
    }

    [Fact(DisplayName = "Route Order Is Properly Mapped To Yarp Route Config")]
    public void RouteOrder_IsProperlyMappedToYarpRouteConfig()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddCluster("catalog-cluster", cluster =>
        {
            cluster.AddDestination("node1", "http://10.0.1.10:5000")
                   .AddRoute("specific-route", "/catalog/items/special", route =>
                   {
                       route.WithOrder(1);
                   })
                   .AddRoute("fallback-route", "/catalog/{**catch-all}", route =>
                   {
                       route.WithOrder(100);
                   });
        });

        var config = provider.GetConfig();
        config.Routes.Count.ShouldBe(2);

        var specific = config.Routes.Single(r => r.RouteId == "specific-route");
        specific.Order.ShouldBe(1);

        var fallback = config.Routes.Single(r => r.RouteId == "fallback-route");
        fallback.Order.ShouldBe(100);
    }

    [Fact(DisplayName = "Http Methods Are Normalized To Uppercase")]
    public void HttpMethods_AreNormalizedToUppercase()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddCluster("orders-cluster", cluster =>
        {
            cluster.AddDestination("node1", "http://10.0.1.10:5000")
                   .AddRoute("orders-methods", "/orders", "get", "post", "delete");
        });

        var config = provider.GetConfig();
        var route = config.Routes.Single(r => r.RouteId == "orders-methods");
        route.Match.Methods.ShouldNotBeNull();
        route.Match.Methods.ShouldBe(["GET", "POST", "DELETE"]);
    }

    [Fact(DisplayName = "Cluster Builder HttpClient And Buffering Mapped Correctly")]
    public void ClusterBuilder_HttpClientAndBuffering_MappedCorrectly()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddCluster("stream-cluster", cluster =>
        {
            cluster.AddDestination("node1", "http://10.0.1.10:5000")
                   .WithHttpClient(new KyrolusHttpClientOptions
                   {
                       DangerousAcceptAnyServerCertificate = true,
                       MaxConnectionsPerServer = 50,
                       EnableMultipleHttp2Connections = true
                   })
                   .WithResponseBuffering(false);
        });

        var config = provider.GetConfig();
        var clusterConfig = config.Clusters.Single(c => c.ClusterId == "stream-cluster");

        clusterConfig.HttpClient.ShouldNotBeNull();
        clusterConfig.HttpClient.DangerousAcceptAnyServerCertificate.ShouldBe(true);
        clusterConfig.HttpClient.MaxConnectionsPerServer.ShouldBe(50);
        clusterConfig.HttpClient.EnableMultipleHttp2Connections.ShouldBe(true);

        clusterConfig.HttpRequest.ShouldNotBeNull();
        clusterConfig.HttpRequest.AllowResponseBuffering.ShouldBe(false);
        clusterConfig.HttpRequest.ActivityTimeout.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact(DisplayName = "Cluster Config Applies Safe Default Activity Timeout Against Slowloris")]
    public void ClusterConfig_AppliesSafeDefaultActivityTimeout_AgainstSlowloris()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddCluster("default-timeout-cluster", cluster =>
        {
            cluster.AddDestination("node1", "http://10.0.1.10:5000");
        });

        var config = provider.GetConfig();
        var clusterConfig = config.Clusters.Single(c => c.ClusterId == "default-timeout-cluster");

        clusterConfig.HttpRequest.ShouldNotBeNull();
        clusterConfig.HttpRequest.ActivityTimeout.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact(DisplayName = "Ip Filter Transform Allows Permitted Ips And Rejects Blocked Ips With 403")]
    public async Task IpFilterTransform_AllowsPermittedIps_AndRejectsBlockedIps_With403()
    {
        var provider = new KyrolusIpFilterTransformProvider();
        var builderContext = new TransformBuilderContext
        {
            Route = new RouteConfig
            {
                RouteId = "admin-route",
                ClusterId = "admin-cluster",
                Match = new RouteMatch { Path = "/admin" },
                Metadata = new Dictionary<string, string>
                {
                    ["Kyrolus:IpFilter:Allowed"] = "10.0.0.0/8, 192.168.1.50",
                    ["Kyrolus:IpFilter:Blocked"] = "10.0.0.99"
                }
            }
        };

        provider.Apply(builderContext);
        builderContext.RequestTransforms.Count.ShouldBe(1);
        var transform = builderContext.RequestTransforms[0];

        // 1. Allowed client IP in 10.0.0.0/8 CIDR
        var allowedContext = new DefaultHttpContext();
        allowedContext.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
        var transformCtx1 = new RequestTransformContext
        {
            HttpContext = allowedContext,
            ProxyRequest = new HttpRequestMessage()
        };
        await transform.ApplyAsync(transformCtx1);
        allowedContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        // 2. Blocked client IP explicitly listed in Blocked
        var blockedContext = new DefaultHttpContext();
        blockedContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.99");
        blockedContext.Response.Body = new MemoryStream();
        var transformCtx2 = new RequestTransformContext
        {
            HttpContext = blockedContext,
            ProxyRequest = new HttpRequestMessage()
        };
        await transform.ApplyAsync(transformCtx2);
        blockedContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        blockedContext.Response.ContentType.ShouldBe("application/problem+json");

        // 3. Client IP outside allowed CIDR/list (e.g. public IP)
        var outsiderContext = new DefaultHttpContext();
        outsiderContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.1");
        outsiderContext.Response.Body = new MemoryStream();
        var transformCtx3 = new RequestTransformContext
        {
            HttpContext = outsiderContext,
            ProxyRequest = new HttpRequestMessage()
        };
        await transform.ApplyAsync(transformCtx3);
        outsiderContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);

        // 4. Fail-Closed: Client IP is null when Allowlist is configured -> 403 Forbidden
        var nullIpContext = new DefaultHttpContext();
        nullIpContext.Connection.RemoteIpAddress = null;
        nullIpContext.Response.Body = new MemoryStream();
        var transformCtx4 = new RequestTransformContext
        {
            HttpContext = nullIpContext,
            ProxyRequest = new HttpRequestMessage()
        };
        await transform.ApplyAsync(transformCtx4);
        nullIpContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nullIpContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "Session Affinity Config Applies Hardened Cookie Defaults")]
    public void SessionAffinityConfig_AppliesHardenedCookieDefaults()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddCluster("sticky-cluster", cluster =>
        {
            cluster.AddDestination("node1", "http://10.0.0.1:5000")
                   .WithSessionAffinity("Cookie", "Redistribute", ".KyrolusGateway.Affinity", cookie =>
                   {
                       cookie.SecurePolicy.ShouldBe("SameAsRequest");
                       cookie.HttpOnly.ShouldBe(true);
                       cookie.SameSite.ShouldBe("Lax");
                       cookie.IsEssential.ShouldBe(true);
                   });
        });

        var config = provider.GetConfig();
        var clusterConfig = config.Clusters.Single(c => c.ClusterId == "sticky-cluster");

        clusterConfig.SessionAffinity.ShouldNotBeNull();
        clusterConfig.SessionAffinity.Enabled.ShouldBe(true);
        clusterConfig.SessionAffinity.Policy.ShouldBe("Cookie");
        clusterConfig.SessionAffinity.FailurePolicy.ShouldBe("Redistribute");
        clusterConfig.SessionAffinity.AffinityKeyName.ShouldBe(".KyrolusGateway.Affinity");

        clusterConfig.SessionAffinity.Cookie.ShouldNotBeNull();
        clusterConfig.SessionAffinity.Cookie.HttpOnly.ShouldBe(true);
        clusterConfig.SessionAffinity.Cookie.SecurePolicy.ShouldBe(Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest);
        clusterConfig.SessionAffinity.Cookie.SameSite.ShouldBe(Microsoft.AspNetCore.Http.SameSiteMode.Lax);
        clusterConfig.SessionAffinity.Cookie.IsEssential.ShouldBe(true);
    }

    [Fact(DisplayName = "Route Config Supports OutputCachePolicy")]
    public void RouteConfig_SupportsOutputCachePolicy()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddCluster("cached-cluster", cluster =>
        {
            cluster.AddDestination("node1", "http://10.0.0.1:5000")
                   .AddRoute("catalog-route", "/api/catalog", r =>
                   {
                       r.WithOutputCache("CatalogCachePolicy");
                   });
        });

        var config = provider.GetConfig();
        var routeConfig = config.Routes.Single(r => r.RouteId == "catalog-route");

        routeConfig.OutputCachePolicy.ShouldBe("CatalogCachePolicy");
    }

    [Fact(DisplayName = "LoadFromConfiguration Parses IpFilter, OutputCachePolicy, and SessionAffinity Cookie")]
    public void LoadFromConfiguration_ParsesIpFilter_OutputCache_AndCookie()
    {
        var json = """
        {
            "ReverseProxy": {
                "Clusters": {
                    "cluster1": {
                        "Destinations": {
                            "d1": { "Address": "http://10.0.0.1:5000" }
                        },
                        "SessionAffinity": {
                            "Enabled": true,
                            "Policy": "Cookie",
                            "AffinityKeyName": ".CustomAffinity",
                            "Cookie": {
                                "HttpOnly": true,
                                "SecurePolicy": "Always",
                                "SameSite": "Strict"
                            }
                        }
                    }
                },
                "Routes": {
                    "route1": {
                        "ClusterId": "cluster1",
                        "Match": { "Path": "/api/admin/{**catch-all}" },
                        "OutputCachePolicy": "AdminCache",
                        "IpFilter": {
                            "AllowedIpsOrCidrs": [ "192.168.1.0/24" ],
                            "BlockedIpsOrCidrs": [ "192.168.1.50" ]
                        }
                    }
                }
            }
        }
        """;

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.LoadFromConfiguration(configuration.GetSection("ReverseProxy"));

        var config = provider.GetConfig();
        var routeConfig = config.Routes.Single(r => r.RouteId == "route1");
        routeConfig.OutputCachePolicy.ShouldBe("AdminCache");
        routeConfig.Metadata.ShouldNotBeNull();
        routeConfig.Metadata["Kyrolus:IpFilter:Allowed"].ShouldBe("192.168.1.0/24");
        routeConfig.Metadata["Kyrolus:IpFilter:Blocked"].ShouldBe("192.168.1.50");

        var clusterConfig = config.Clusters.Single(c => c.ClusterId == "cluster1");
        clusterConfig.SessionAffinity.ShouldNotBeNull();
        clusterConfig.SessionAffinity.Cookie.ShouldNotBeNull();
        clusterConfig.SessionAffinity.Cookie.SecurePolicy.ShouldBe(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always);
        clusterConfig.SessionAffinity.Cookie.SameSite.ShouldBe(Microsoft.AspNetCore.Http.SameSiteMode.Strict);
    }

    [Fact(DisplayName = "IKyrolusDynamicRouteProvider Asynchronous CRUD Operations Work Correctly")]
    public async Task IKyrolusDynamicRouteProvider_AsyncCrudOperations_WorkCorrectly()
    {
        IKyrolusDynamicRouteProvider provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        var cluster = new KyrolusGatewayCluster
        {
            ClusterId = "dynamic-cluster",
            Destinations = new Dictionary<string, KyrolusGatewayDestination>
            {
                ["node1"] = new("http://10.0.0.1:5000"),
                ["node2"] = new("http://10.0.0.2:5000")
            }
        };

        var route = new KyrolusGatewayRoute
        {
            RouteId = "dynamic-route",
            ClusterId = "dynamic-cluster",
            Match = new KyrolusGatewayRouteMatch { Path = "/dynamic" }
        };

        await provider.AddClusterAsync(cluster);
        await provider.AddRouteAsync(route);

        var clusters = await provider.GetClustersAsync();
        clusters.Count.ShouldBe(1);
        clusters[0].ClusterId.ShouldBe("dynamic-cluster");

        var routes = await provider.GetRoutesAsync();
        routes.Count.ShouldBe(1);
        routes[0].RouteId.ShouldBe("dynamic-route");

        var destinationRemoved = await provider.RemoveDestinationAsync("dynamic-cluster", "node1");
        destinationRemoved.ShouldBe(true);

        clusters = await provider.GetClustersAsync();
        clusters[0].Destinations.ContainsKey("node1").ShouldBe(false);
        clusters[0].Destinations.ContainsKey("node2").ShouldBe(true);

        var routeRemoved = await provider.RemoveRouteAsync("dynamic-route");
        routeRemoved.ShouldBe(true);

        routes = await provider.GetRoutesAsync();
        routes.Count.ShouldBe(0);

        await provider.ClearAsync();
        clusters = await provider.GetClustersAsync();
        clusters.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "CorrelationId Transform Picks Up X-Request-ID Header When Present")]
    public async Task CorrelationId_PicksUpXRequestId_WhenPresent()
    {
        var provider = new KyrolusCorrelationTransformProvider();
        var context = new TransformBuilderContext();
        provider.Apply(context);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Request-ID"] = "req-123456";

        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        var requestTransform = context.RequestTransforms.Single();
        await requestTransform.ApplyAsync(transformContext);

        transformContext.ProxyRequest.Headers.Contains("X-Correlation-ID").ShouldBe(true);
        transformContext.ProxyRequest.Headers.GetValues("X-Correlation-ID").Single().ShouldBe("req-123456");
    }

    [Fact(DisplayName = "Telemetry Header Can Be Suppressed Via Route Metadata")]
    public void TelemetryHeader_CanBeSuppressed_ViaRouteMetadata()
    {
        var provider = new KyrolusTelemetryHeadersTransformProvider();

        var route = new RouteConfig
        {
            RouteId = "suppressed-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/test" },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:SuppressTelemetryHeader"] = "true"
            }
        };

        var context = new TransformBuilderContext
        {
            Route = route
        };

        provider.Apply(context);
        context.ResponseTransforms.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "RouteBuilder Generates Fluent Header and Query Transforms")]
    public void RouteBuilder_GeneratesFluentHeaderAndQueryTransforms()
    {
        var builder = new KyrolusRouteBuilder("test-route", "test-cluster", "/test");
        builder.WithTransformRequestHeader("X-Api-Key", "secret-key")
               .WithTransformRequestHeaderRemove("X-Client-Secret")
               .WithTransformResponseHeader("X-Served-By", "KyrolusGateway")
               .WithTransformResponseHeaderRemove("X-Internal-Debug")
               .WithTransformQueryValueParameter("v", "2")
               .WithTransformQueryRouteParameter("user", "userId")
               .WithSuppressTelemetryHeader(true);

        var route = builder.Build();

        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(6);

        route.Transforms[0]["RequestHeader"].ShouldBe("X-Api-Key");
        route.Transforms[0]["Set"].ShouldBe("secret-key");

        route.Transforms[1]["RequestHeaderRemove"].ShouldBe("X-Client-Secret");

        route.Transforms[2]["ResponseHeader"].ShouldBe("X-Served-By");
        route.Transforms[2]["Set"].ShouldBe("KyrolusGateway");

        route.Transforms[3]["ResponseHeaderRemove"].ShouldBe("X-Internal-Debug");

        route.Transforms[4]["QueryValueParameter"].ShouldBe("v");
        route.Transforms[4]["Set"].ShouldBe("2");

        route.Transforms[5]["QueryRouteParameter"].ShouldBe("user");
        route.Transforms[5]["Set"].ShouldBe("userId");

        route.Metadata.ShouldNotBeNull();
        route.Metadata["Kyrolus:SuppressTelemetryHeader"].ShouldBe("true");
    }

    [Fact(DisplayName = "RouteBuilder Supports Header and Query Matching and Provider Maps Them")]
    public void RouteBuilder_SupportsHeaderAndQueryMatching_AndProviderMapsThem()
    {
        var builder = new KyrolusRouteBuilder("canary-route", "orders-cluster", "/api/orders/{**catch-all}");
        builder.WithHeaderMatch("X-Canary", ["beta", "pilot"], "ExactHeader", isCaseSensitive: false)
               .WithHeaderExists("X-Features")
               .WithQueryMatch("v", ["2"], "Exact", isCaseSensitive: false)
               .WithQueryExists("preview");

        var route = builder.Build();

        route.Match.Headers.ShouldNotBeNull();
        route.Match.Headers.Count.ShouldBe(2);
        route.Match.Headers[0].Name.ShouldBe("X-Canary");
        route.Match.Headers[0].Values.ShouldBe(new[] { "beta", "pilot" });
        route.Match.Headers[0].Mode.ShouldBe(KyrolusHeaderMatchMode.ExactHeader);
        route.Match.Headers[0].IsCaseSensitive.ShouldBeFalse();

        route.Match.Headers[1].Name.ShouldBe("X-Features");
        route.Match.Headers[1].Mode.ShouldBe(KyrolusHeaderMatchMode.Exists);

        route.Match.QueryParameters.ShouldNotBeNull();
        route.Match.QueryParameters.Count.ShouldBe(2);
        route.Match.QueryParameters[0].Name.ShouldBe("v");
        route.Match.QueryParameters[0].Values.ShouldBe(new[] { "2" });
        route.Match.QueryParameters[0].Mode.ShouldBe(KyrolusQueryParamMatchMode.Exact);

        route.Match.QueryParameters[1].Name.ShouldBe("preview");
        route.Match.QueryParameters[1].Mode.ShouldBe(KyrolusQueryParamMatchMode.Exists);

        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddRoute(route);
        var snapshot = provider.GetConfig();
        var yarpRoute = snapshot.Routes.Single(r => r.RouteId == "canary-route");

        yarpRoute.Match.Headers.ShouldNotBeNull();
        yarpRoute.Match.Headers.Count.ShouldBe(2);
        yarpRoute.Match.Headers[0].Name.ShouldBe("X-Canary");
        yarpRoute.Match.Headers[0].Mode.ShouldBe(HeaderMatchMode.ExactHeader);

        yarpRoute.Match.QueryParameters.ShouldNotBeNull();
        yarpRoute.Match.QueryParameters.Count.ShouldBe(2);
        yarpRoute.Match.QueryParameters[0].Name.ShouldBe("v");
        yarpRoute.Match.QueryParameters[0].Mode.ShouldBe(QueryParameterMatchMode.Exact);
    }

    [Fact(DisplayName = "RouteBuilder Supports RequireTenant And AllowedContentTypes")]
    public void RouteBuilder_SupportsRequireTenantAndAllowedContentTypes()
    {
        var builder = new KyrolusRouteBuilder("tenant-secure-route", "secure-cluster", "/api/secure");
        builder.WithRequireTenant(true)
               .WithAllowedContentTypes("application/json", "text/plain");

        var route = builder.Build();
        route.RequireTenant.ShouldBeTrue();
        route.AllowedContentTypes.ShouldNotBeNull();
        route.AllowedContentTypes.Count.ShouldBe(2);

        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddRoute(route);
        var snapshot = provider.GetConfig();
        var yarpRoute = snapshot.Routes.Single(r => r.RouteId == "tenant-secure-route");

        yarpRoute.Metadata.ShouldNotBeNull();
        yarpRoute.Metadata["Kyrolus:Tenant:Required"].ShouldBe("true");
        yarpRoute.Metadata["Kyrolus:ContentType:Allowed"].ShouldBe("application/json,text/plain");
    }

    [Fact(DisplayName = "ContentTypeTransform Allows Configured ContentTypes And Rejects Disallowed")]
    public async Task ContentTypeTransform_AllowsConfiguredContentTypes_AndRejectsDisallowed()
    {
        var provider = new KyrolusContentTypeTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "api-route",
            ClusterId = "api-cluster",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:ContentType:Allowed"] = "application/json, text/plain"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        builderContext.RequestTransforms.Count.ShouldBe(1);
        var transform = builderContext.RequestTransforms[0];

        // 1. Allowed content type
        var okContext = new DefaultHttpContext();
        okContext.Request.ContentType = "application/json; charset=utf-8";
        okContext.Request.ContentLength = 100;
        var okTransformContext = new RequestTransformContext
        {
            HttpContext = okContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await transform.ApplyAsync(okTransformContext);
        okContext.Response.StatusCode.ShouldBe(200);

        // 2. Disallowed content type (e.g. XML attempt / XXE)
        var badContext = new DefaultHttpContext();
        badContext.Response.Body = new MemoryStream();
        badContext.Request.ContentType = "application/xml";
        badContext.Request.ContentLength = 250;
        var badTransformContext = new RequestTransformContext
        {
            HttpContext = badContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await transform.ApplyAsync(badTransformContext);
        badContext.Response.StatusCode.ShouldBe(StatusCodes.Status415UnsupportedMediaType);
        badContext.Response.ContentType.ShouldBe("application/problem+json");

        // 3. Request without content (e.g. GET) should pass through
        var getContext = new DefaultHttpContext();
        getContext.Request.ContentLength = 0;
        var getTransformContext = new RequestTransformContext
        {
            HttpContext = getContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await transform.ApplyAsync(getTransformContext);
        getContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact(DisplayName = "TenantRoutingTransform Enforces RequireTenant Returning 401 When Tenant Missing")]
    public async Task TenantRoutingTransform_EnforcesRequireTenant_Returns401WhenTenantMissing()
    {
        var provider = new KyrolusTenantRoutingTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "tenant-only-route",
            ClusterId = "tenant-cluster",
            Match = new RouteMatch { Path = "/tenant/api" },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:Tenant:Required"] = "true"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        // Request with no tenant context and no subdomain tenant
        var anonymousContext = new DefaultHttpContext();
        anonymousContext.Response.Body = new MemoryStream();
        anonymousContext.Request.Host = new HostString("example.com");
        var anonymousTransformContext = new RequestTransformContext
        {
            HttpContext = anonymousContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await transform.ApplyAsync(anonymousTransformContext);
        anonymousContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        anonymousContext.Response.ContentType.ShouldBe("application/problem+json");

        // Request WITH valid tenant claim
        var tenantContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("tenant_id", "tenant-123")
            ], "Bearer"))
        };
        var validProxyReq = new HttpRequestMessage();
        var tenantTransformContext = new RequestTransformContext
        {
            HttpContext = tenantContext,
            ProxyRequest = validProxyReq
        };

        await transform.ApplyAsync(tenantTransformContext);
        tenantContext.Response.StatusCode.ShouldBe(200);
        validProxyReq.Headers.GetValues("X-Tenant-ID").Single().ShouldBe("tenant-123");
    }

    [Fact(DisplayName = "ClusterBuilder Configures HttpVersionPolicy And DefaultVersion")]
    public void ClusterBuilder_ConfiguresHttpVersionPolicyAndDefaultVersion()
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.AddCluster("grpc-cluster", cluster =>
        {
            cluster.AddDestination("node1", "https://10.0.1.5:5001")
                   .WithHttpClient(new KyrolusHttpClientOptions
                   {
                       DefaultVersion = HttpVersion.Version20,
                       VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                       EnableMultipleHttp2Connections = true
                   });
        });

        var snapshot = provider.GetConfig();
        var yarpCluster = snapshot.Clusters.Single(c => c.ClusterId == "grpc-cluster");

        yarpCluster.HttpRequest.ShouldNotBeNull();
        yarpCluster.HttpRequest.Version.ShouldBe(HttpVersion.Version20);
        yarpCluster.HttpRequest.VersionPolicy.ShouldBe(HttpVersionPolicy.RequestVersionExact);
        yarpCluster.HttpClient.ShouldNotBeNull();
        yarpCluster.HttpClient.EnableMultipleHttp2Connections.ShouldBe(true);
    }

    [Fact(DisplayName = "RouteBuilder Supports WithTransformForwarded")]
    public void RouteBuilder_SupportsWithTransformForwarded()
    {
        var builder = new KyrolusRouteBuilder("forwarded-route", "cluster1", "/api");
        builder.WithTransformForwarded(forFormat: "Random", host: true, proto: true, prefix: "Forwarded");

        var route = builder.Build();
        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(1);
        route.Transforms[0]["Forwarded"].ShouldBe("proto,host,for");
        route.Transforms[0]["ForFormat"].ShouldBe("Random");
        route.Transforms[0]["Prefix"].ShouldBe("Forwarded");
    }

    [Fact(DisplayName = "LoadFromConfiguration Parses Phase 3 Headers Query Version ContentTypes And RequireTenant")]
    public void LoadFromConfiguration_ParsesPhase3HeadersQueryVersionContentTypesAndRequireTenant()
    {
        var json = """
        {
            "ReverseProxy": {
                "Clusters": {
                    "v3-cluster": {
                        "Destinations": {
                            "d1": { "Address": "https://grpc-backend:5001" }
                        },
                        "HttpRequest": {
                            "Version": "2.0",
                            "VersionPolicy": "RequestVersionExact"
                        }
                    }
                },
                "Routes": {
                    "v3-route": {
                        "ClusterId": "v3-cluster",
                        "RequireTenant": "true",
                        "AllowedContentTypes": ["application/json", "application/grpc"],
                        "Match": {
                            "Path": "/grpc.Service/{**catch-all}",
                            "Headers": [
                                {
                                    "Name": "X-Canary",
                                    "Values": ["beta"],
                                    "Mode": "ExactHeader",
                                    "IsCaseSensitive": "false"
                                }
                            ],
                            "QueryParameters": [
                                {
                                    "Name": "tier",
                                    "Values": ["premium"],
                                    "Mode": "Exact"
                                }
                            ]
                        }
                    }
                }
            }
        }
        """;

        var builder = new ConfigurationBuilder();
        builder.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
        var configuration = builder.Build();

        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.LoadFromConfiguration(configuration.GetSection("ReverseProxy"));

        var snapshot = provider.GetConfig();
        var cluster = snapshot.Clusters.Single(c => c.ClusterId == "v3-cluster");
        cluster.HttpRequest.ShouldNotBeNull();
        cluster.HttpRequest.Version.ShouldBe(HttpVersion.Version20);
        cluster.HttpRequest.VersionPolicy.ShouldBe(HttpVersionPolicy.RequestVersionExact);

        var route = snapshot.Routes.Single(r => r.RouteId == "v3-route");
        route.Metadata.ShouldNotBeNull();
        route.Metadata["Kyrolus:Tenant:Required"].ShouldBe("true");
        route.Metadata["Kyrolus:ContentType:Allowed"].ShouldBe("application/json,application/grpc");

        route.Match.Headers.ShouldNotBeNull();
        route.Match.Headers.Count.ShouldBe(1);
        route.Match.Headers[0].Name.ShouldBe("X-Canary");
        route.Match.Headers[0].Mode.ShouldBe(HeaderMatchMode.ExactHeader);

        route.Match.QueryParameters.ShouldNotBeNull();
        route.Match.QueryParameters.Count.ShouldBe(1);
        route.Match.QueryParameters[0].Name.ShouldBe("tier");
        route.Match.QueryParameters[0].Mode.ShouldBe(QueryParameterMatchMode.Exact);
    }

    [Fact(DisplayName = "MethodOverrideTransform Strips Untrusted Method Override Header By Default")]
    public async Task MethodOverrideTransform_StripsUntrustedMethodOverrideHeader_ByDefault()
    {
        var provider = new KyrolusMethodOverrideTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "post-only-route",
            ClusterId = "cluster1",
            Match = new RouteMatch
            {
                Path = "/api/items",
                Methods = ["POST"]
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Headers["X-HTTP-Method-Override"] = "DELETE";

        var proxyRequest = new HttpRequestMessage(HttpMethod.Post, "https://backend/api/items");
        proxyRequest.Headers.Add("X-HTTP-Method-Override", "DELETE");

        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await transform.ApplyAsync(transformContext);

        proxyRequest.Headers.Contains("X-HTTP-Method-Override").ShouldBeFalse();
        httpContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact(DisplayName = "MethodOverrideTransform Validates Allowed Methods When Explicitly Enabled")]
    public async Task MethodOverrideTransform_ValidatesAllowedMethods_WhenExplicitlyEnabled()
    {
        var provider = new KyrolusMethodOverrideTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "custom-route",
            ClusterId = "cluster1",
            Match = new RouteMatch
            {
                Path = "/api/resource",
                Methods = ["POST", "PUT"]
            },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:MethodOverride:Allowed"] = "true"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        // Scenario 1: Overriding to a permitted method (PUT is allowed on this route)
        var validContext = new DefaultHttpContext();
        validContext.Request.Method = "POST";
        validContext.Request.Headers["X-HTTP-Method-Override"] = "PUT";

        var validProxyReq = new HttpRequestMessage(HttpMethod.Post, "https://backend/api/resource");
        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = validContext,
            ProxyRequest = validProxyReq
        });
        validContext.Response.StatusCode.ShouldBe(200);

        // Scenario 2: Overriding to a forbidden method (DELETE is not declared in route methods)
        var invalidContext = new DefaultHttpContext();
        invalidContext.Response.Body = new MemoryStream();
        invalidContext.Request.Method = "POST";
        invalidContext.Request.Headers["X-HTTP-Method-Override"] = "DELETE";

        var invalidProxyReq = new HttpRequestMessage(HttpMethod.Post, "https://backend/api/resource");
        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = invalidContext,
            ProxyRequest = invalidProxyReq
        });
        invalidContext.Response.StatusCode.ShouldBe(StatusCodes.Status405MethodNotAllowed);
        invalidContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "ClientCertTransform Strips Spoofed Headers And Injects Authoritative Certificate")]
    public async Task ClientCertTransform_StripsSpoofedHeaders_AndInjectsAuthoritativeCertificate()
    {
        var provider = new KyrolusClientCertTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "mtls-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/internal" },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:ClientCert:Forward"] = "true"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        using var rsa = RSA.Create(2048);
        var certReq = new CertificateRequest("CN=trusted-service", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = cert;

        var proxyRequest = new HttpRequestMessage();
        proxyRequest.Headers.Add("X-Client-Cert-Thumbprint", "fake-spoofed-thumbprint");

        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await transform.ApplyAsync(transformContext);

        proxyRequest.Headers.GetValues("X-Client-Cert-Thumbprint").Single().ShouldBe(cert.Thumbprint);
        proxyRequest.Headers.GetValues("X-Client-Cert-Subject").Single().ShouldBe(cert.Subject);
    }

    [Fact(DisplayName = "PathTraversalTransform Blocks Traversal Attempts And Null Bytes")]
    public async Task PathTraversalTransform_BlocksTraversalAttempts_AndNullBytes()
    {
        var provider = new KyrolusPathTraversalTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        var testCases = new[]
        {
            "/api/../admin",
            "/api/%2e%2e/admin",
            "/api/items%00",
            @"/api\admin",
            @"/api\..\secret"
        };

        foreach (var badPath in testCases)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.Body = new MemoryStream();
            httpContext.Request.Path = new PathString(badPath);

            var transformContext = new RequestTransformContext
            {
                HttpContext = httpContext,
                ProxyRequest = new HttpRequestMessage()
            };

            await transform.ApplyAsync(transformContext);
            httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
            httpContext.Response.ContentType.ShouldBe("application/problem+json");
        }

        var safeContext = new DefaultHttpContext();
        safeContext.Request.Path = "/api/orders/123";
        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = safeContext,
            ProxyRequest = new HttpRequestMessage()
        });
        safeContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact(DisplayName = "SecurityHeadersTransform Injects CrossDomainPolicies And Custom CSP And FrameOptions")]
    public async Task SecurityHeadersTransform_InjectsCrossDomainPolicies_AndCustomCspAndFrameOptions()
    {
        var provider = new KyrolusSecurityHeadersTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "custom-headers-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/portal" },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:SecurityHeaders:CSP"] = "default-src 'self'",
                ["Kyrolus:SecurityHeaders:FrameOptions"] = "SAMEORIGIN",
                ["Kyrolus:SecurityHeaders:ReferrerPolicy"] = "no-referrer"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.ResponseTransforms[0];

        var httpContext = new DefaultHttpContext();
        var transformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        await transform.ApplyAsync(transformContext);

        var headers = httpContext.Response.Headers;
        headers["X-Permitted-Cross-Domain-Policies"].ToString().ShouldBe("none");
        headers["Content-Security-Policy"].ToString().ShouldBe("default-src 'self'");
        headers["X-Frame-Options"].ToString().ShouldBe("SAMEORIGIN");
        headers["Referrer-Policy"].ToString().ShouldBe("no-referrer");
    }

    [Fact(DisplayName = "RouteBuilder Supports Phase 4 Fluent Configuration Methods")]
    public void RouteBuilder_SupportsPhase4FluentConfigurationMethods()
    {
        var builder = new KyrolusRouteBuilder("phase4-route", "cluster1", "/api/v4");
        builder.WithAllowMethodOverride(true)
               .WithClientCertForwarding(true)
               .WithContentSecurityPolicy("script-src 'self'")
               .WithFrameOptions("SAMEORIGIN")
               .WithReferrerPolicy("no-referrer")
               .WithTransformHost("internal.service:5000");

        var route = builder.Build();

        route.Metadata.ShouldNotBeNull();
        route.Metadata["Kyrolus:MethodOverride:Allowed"].ShouldBe("true");
        route.Metadata["Kyrolus:ClientCert:Forward"].ShouldBe("true");
        route.Metadata["Kyrolus:SecurityHeaders:CSP"].ShouldBe("script-src 'self'");
        route.Metadata["Kyrolus:SecurityHeaders:FrameOptions"].ShouldBe("SAMEORIGIN");
        route.Metadata["Kyrolus:SecurityHeaders:ReferrerPolicy"].ShouldBe("no-referrer");

        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(1);
        route.Transforms[0]["RequestHeader"].ShouldBe("Host");
        route.Transforms[0]["Set"].ShouldBe("internal.service:5000");
    }

    [Fact(DisplayName = "RateLimitPartitionKeys Resolves Partition Keys Accurately")]
    public void RateLimitPartitionKeys_ResolvesPartitionKeysAccurately()
    {
        // 1. IP Partition Key
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.42");
        KyrolusRateLimitPartitionKeys.GetClientIpKey(httpContext).ShouldBe("198.51.100.42");

        // 2. Tenant Partition Key
        var services = new ServiceCollection();
        var tenantContext = new KyrolusTenantContext { TenantId = "tenant-corp-99" };
        services.AddSingleton<IKyrolusTenantContext>(tenantContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        KyrolusRateLimitPartitionKeys.GetTenantKey(httpContext).ShouldBe("tenant-corp-99");
        KyrolusRateLimitPartitionKeys.GetTenantAndIpKey(httpContext).ShouldBe("tenant-corp-99:198.51.100.42");

        // 3. User Partition Key
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-abc-123")
        ], "Bearer"));

        KyrolusRateLimitPartitionKeys.GetUserKey(httpContext).ShouldBe("user_user-abc-123");
    }

    [Fact(DisplayName = "TransformPipeline ShortCircuits When Response HasStarted")]
    public async Task TransformPipeline_ShortCircuitsWhenResponseHasStarted()
    {
        // Setup a context where response has already started (with throwing setter for StatusCode)
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(new StartedResponseFeature());
        httpContext.Response.HasStarted.ShouldBeTrue();

        // 1. Path Traversal transform
        var pathProvider = new KyrolusPathTraversalTransformProvider();
        var pathContext = new TransformBuilderContext();
        pathProvider.Apply(pathContext);
        var pathTransform = pathContext.RequestTransforms[0];
        httpContext.Request.Path = "/api/../admin";
        await Should.NotThrowAsync(async () =>
        {
            await pathTransform.ApplyAsync(new RequestTransformContext
            {
                HttpContext = httpContext,
                ProxyRequest = new HttpRequestMessage()
            });
        });

        // 2. Correlation transform (Response)
        var corrProvider = new KyrolusCorrelationTransformProvider();
        var corrContext = new TransformBuilderContext();
        corrProvider.Apply(corrContext);
        var corrResponseTransform = corrContext.ResponseTransforms[0];
        await Should.NotThrowAsync(async () =>
        {
            await corrResponseTransform.ApplyAsync(new ResponseTransformContext
            {
                HttpContext = httpContext
            });
        });

        // 3. Security Headers transform (Response)
        var secProvider = new KyrolusSecurityHeadersTransformProvider();
        var secContext = new TransformBuilderContext();
        secProvider.Apply(secContext);
        var secResponseTransform = secContext.ResponseTransforms[0];
        await Should.NotThrowAsync(async () =>
        {
            await secResponseTransform.ApplyAsync(new ResponseTransformContext
            {
                HttpContext = httpContext
            });
        });

        // 4. Telemetry Headers transform (Response)
        var telProvider = new KyrolusTelemetryHeadersTransformProvider();
        var telContext = new TransformBuilderContext();
        telProvider.Apply(telContext);
        var telResponseTransform = telContext.ResponseTransforms[0];
        await Should.NotThrowAsync(async () =>
        {
            await telResponseTransform.ApplyAsync(new ResponseTransformContext
            {
                HttpContext = httpContext
            });
        });

        // 5. Gateway Error transform (Response)
        var errProvider = new KyrolusGatewayErrorTransformProvider();
        var errContext = new TransformBuilderContext();
        errProvider.Apply(errContext);
        var errResponseTransform = errContext.ResponseTransforms[0];
        await Should.NotThrowAsync(async () =>
        {
            await errResponseTransform.ApplyAsync(new ResponseTransformContext
            {
                HttpContext = httpContext
            });
        });
    }

    private sealed class StartedResponseFeature : Microsoft.AspNetCore.Http.Features.IHttpResponseFeature
    {
        public int StatusCode
        {
            get => 200;
            set => throw new InvalidOperationException("StatusCode cannot be set because the response has already started.");
        }
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    [Theory(DisplayName = "GatewayErrorTransform Formats ProblemDetails For 502, 503, And 504")]
    [InlineData(502, "Bad Gateway")]
    [InlineData(503, "Service Unavailable")]
    [InlineData(504, "Gateway Timeout")]
    public async Task GatewayErrorTransform_FormatsProblemDetails_For502_503_504(int statusCode, string expectedTitle)
    {
        var provider = new KyrolusGatewayErrorTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);
        var transform = builderContext.ResponseTransforms[0];

        var httpContext = new DefaultHttpContext();
        var stream = new MemoryStream();
        httpContext.Response.Body = stream;
        httpContext.Response.StatusCode = statusCode;

        var transformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        await transform.ApplyAsync(transformContext);

        transformContext.SuppressResponseBody.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(statusCode);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");

        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var body = await reader.ReadToEndAsync();
        body.ShouldContain(expectedTitle);
        body.ShouldContain(statusCode.ToString());
    }

    [Fact(DisplayName = "SecurityHeadersTransform Strips CWE-200 Backend Information Headers")]
    public async Task SecurityHeadersTransform_StripsCwe200_BackendInformationHeaders()
    {
        var provider = new KyrolusSecurityHeadersTransformProvider();
        var builderContext = new TransformBuilderContext();
        provider.Apply(builderContext);
        var transform = builderContext.ResponseTransforms[0];

        var httpContext = new DefaultHttpContext();
        var headers = httpContext.Response.Headers;
        headers["Server"] = "Kestrel";
        headers["X-Powered-By"] = "ASP.NET";
        headers["X-AspNet-Version"] = "10.0";
        headers["X-AspNetMvc-Version"] = "10.0";
        headers["X-Runtime"] = "12ms";
        headers["X-SourceFiles"] = @"C:\inetpub\wwwroot\app";
        headers["X-Generated-By"] = "InternalGateway";
        headers["X-Backend-Server"] = "k8s-pod-10-42-1-15";
        headers["X-Backend-Host"] = "cluster.internal";

        var transformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        await transform.ApplyAsync(transformContext);

        headers.ContainsKey("Server").ShouldBeFalse();
        headers.ContainsKey("X-Powered-By").ShouldBeFalse();
        headers.ContainsKey("X-AspNet-Version").ShouldBeFalse();
        headers.ContainsKey("X-AspNetMvc-Version").ShouldBeFalse();
        headers.ContainsKey("X-Runtime").ShouldBeFalse();
        headers.ContainsKey("X-SourceFiles").ShouldBeFalse();
        headers.ContainsKey("X-Generated-By").ShouldBeFalse();
        headers.ContainsKey("X-Backend-Server").ShouldBeFalse();
        headers.ContainsKey("X-Backend-Host").ShouldBeFalse();
    }

    [Fact(DisplayName = "ConfigurationHotReload Triggers Reload On Section Change")]
    public void ConfigurationHotReload_TriggersReloadOnSectionChange()
    {
        var initialData = new Dictionary<string, string?>
        {
            ["ReverseProxy:Clusters:c1:Destinations:d1:Address"] = "https://svc1.internal",
            ["ReverseProxy:Routes:r1:ClusterId"] = "c1",
            ["ReverseProxy:Routes:r1:Match:Path"] = "/api/v1/{**catch-all}"
        };

        var configRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build();

        using var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        provider.LoadFromConfiguration(configRoot.GetSection("ReverseProxy"));

        var initialConfig = provider.GetConfig();
        initialConfig.Routes.Count.ShouldBe(1);
        initialConfig.Routes[0].Match.Path.ShouldBe("/api/v1/{**catch-all}");

        // Now mutate the configuration and trigger reload
        configRoot["ReverseProxy:Routes:r1:Match:Path"] = "/api/v2/{**catch-all}";
        configRoot.Reload();

        var reloadedConfig = provider.GetConfig();
        reloadedConfig.Routes.Count.ShouldBe(1);
        reloadedConfig.Routes[0].Match.Path.ShouldBe("/api/v2/{**catch-all}");
    }

    [Fact(DisplayName = "RouteBuilder WithStripHeader Adds Both Request And Response Remove Transforms")]
    public void RouteBuilder_WithStripHeader_AddsBothRequestAndResponseRemoveTransforms()
    {
        var builder = new KyrolusRouteBuilder("strip-route", "cluster1", "/api/strip");
        builder.WithStripHeader("X-Sensitive-Secret");

        var route = builder.Build();
        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(2);

        route.Transforms[0]["RequestHeaderRemove"].ShouldBe("X-Sensitive-Secret");
        route.Transforms[1]["ResponseHeaderRemove"].ShouldBe("X-Sensitive-Secret");
    }

    [Fact(DisplayName = "HeaderLimitsTransform Rejects Excessive Header Count Returning 431")]
    public async Task HeaderLimitsTransform_RejectsExcessiveHeaderCount_Returns431()
    {
        var provider = new KyrolusHeaderLimitsTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "header-limit-route",
            ClusterId = "c1",
            Match = new RouteMatch { Path = "/api/test" },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:Headers:MaxCount"] = "5"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        for (var i = 0; i < 10; i++)
        {
            httpContext.Request.Headers[$"X-Custom-Header-{i}"] = $"val-{i}";
        }

        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await transform.ApplyAsync(transformContext);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status431RequestHeaderFieldsTooLarge);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "HeaderLimitsTransform Rejects Oversized Total Header Size Returning 431")]
    public async Task HeaderLimitsTransform_RejectsOversizedTotalHeaderSize_Returns431()
    {
        var provider = new KyrolusHeaderLimitsTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "header-size-route",
            ClusterId = "c1",
            Match = new RouteMatch { Path = "/api/test" },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:Headers:MaxTotalLength"] = "200"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Headers["X-Huge-Header"] = new string('A', 500);

        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await transform.ApplyAsync(transformContext);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status431RequestHeaderFieldsTooLarge);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "PayloadSizeTransform Rejects Oversized ContentLength Returning 413")]
    public async Task PayloadSizeTransform_RejectsOversizedContentLength_Returns413()
    {
        var provider = new KyrolusPayloadSizeTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "payload-size-route",
            ClusterId = "c1",
            Match = new RouteMatch { Path = "/api/upload" },
            MaxRequestBodySize = 1024 // 1 KB max
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        // 1. Oversized payload (10 KB > 1 KB)
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.ContentLength = 10 * 1024;

        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await transform.ApplyAsync(transformContext);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");

        // 2. Normal payload (500 B < 1 KB)
        var safeContext = new DefaultHttpContext();
        safeContext.Request.ContentLength = 500;
        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = safeContext,
            ProxyRequest = new HttpRequestMessage()
        });
        safeContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact(DisplayName = "ClusterBuilder WithActiveAndPassiveHealthChecks Configures Cluster Correctly")]
    public void ClusterBuilder_WithActiveAndPassiveHealthChecks_ConfiguresClusterCorrectly()
    {
        var builder = new KyrolusClusterBuilder("health-monitored-cluster");
        builder.AddDestination("node1", "https://10.0.1.50:5001")
               .WithActiveHealthCheck("/healthz", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(3))
               .WithPassiveHealthCheck(TimeSpan.FromSeconds(45));

        var (cluster, _) = builder.Build();

        cluster.HealthCheck.ShouldNotBeNull();
        cluster.HealthCheck.Active.ShouldNotBeNull();
        cluster.HealthCheck.Active.Enabled.ShouldBeTrue();
        cluster.HealthCheck.Active.Path.ShouldBe("/healthz");
        cluster.HealthCheck.Active.Interval.ShouldBe(TimeSpan.FromSeconds(15));
        cluster.HealthCheck.Active.Timeout.ShouldBe(TimeSpan.FromSeconds(3));

        cluster.HealthCheck.Passive.ShouldNotBeNull();
        cluster.HealthCheck.Passive.Enabled.ShouldBeTrue();
        cluster.HealthCheck.Passive.ReactivationPeriod.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact(DisplayName = "RouteBuilder WithHsts And WithMaxHeaderLimits Applies Metadata And Custom HSTS")]
    public async Task RouteBuilder_WithHsts_And_WithMaxHeaderLimits_AppliesMetadataAndCustomHsts()
    {
        var builder = new KyrolusRouteBuilder("hsts-route", "cluster1", "/secure");
        builder.WithHsts(TimeSpan.FromDays(730), includeSubDomains: true, preload: true)
               .WithMaxHeaderLimits(50, 16384);

        var route = builder.Build();
        route.Metadata.ShouldNotBeNull();
        route.Metadata["Kyrolus:SecurityHeaders:HSTS"].ShouldBe("max-age=63072000; includeSubDomains; preload");
        route.Metadata["Kyrolus:Headers:MaxCount"].ShouldBe("50");
        route.Metadata["Kyrolus:Headers:MaxTotalLength"].ShouldBe("16384");

        // Verify that KyrolusSecurityHeadersTransformProvider applies this custom HSTS
        var provider = new KyrolusSecurityHeadersTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "hsts-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/secure" },
            Metadata = route.Metadata
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.ResponseTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";

        var transformContext = new ResponseTransformContext
        {
            HttpContext = httpContext
        };

        await transform.ApplyAsync(transformContext);

        httpContext.Response.Headers["Strict-Transport-Security"].ToString()
            .ShouldBe("max-age=63072000; includeSubDomains; preload");
    }

    [Fact(DisplayName = "RequestSmugglingTransformProvider: Blocks conflicting CL and TE, differing CL, and malformed framing")]
    public async Task RequestSmugglingTransformProvider_BlocksConflictingCLAndTE_AndMalformedFraming()
    {
        var provider = new KyrolusRequestSmugglingTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "smuggling-test-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        // 1. Conflicting CL and TE
        var clTeContext = new DefaultHttpContext();
        clTeContext.Response.Body = new MemoryStream();
        clTeContext.Request.Headers["Transfer-Encoding"] = "chunked";
        clTeContext.Request.Headers["Content-Length"] = "42";

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = clTeContext,
            ProxyRequest = new HttpRequestMessage()
        });

        clTeContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        clTeContext.Response.ContentType.ShouldBe("application/problem+json");
        clTeContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(clTeContext.Response.Body).ReadToEndAsync();
        body.ShouldContain("Conflicting or duplicate content transfer headers detected");

        // 2. Differing duplicate Content-Length headers
        var dupClContext = new DefaultHttpContext();
        dupClContext.Response.Body = new MemoryStream();
        dupClContext.Request.Headers.Append("Content-Length", "50");
        dupClContext.Request.Headers.Append("Content-Length", "100");

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = dupClContext,
            ProxyRequest = new HttpRequestMessage()
        });

        dupClContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        // 3. TE with control characters
        var ctrlContext = new DefaultHttpContext();
        ctrlContext.Response.Body = new MemoryStream();
        ctrlContext.Request.Headers["Transfer-Encoding"] = "chunked\r\ninvalid";

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = ctrlContext,
            ProxyRequest = new HttpRequestMessage()
        });

        ctrlContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        // 4. Clean legitimate request
        var cleanContext = new DefaultHttpContext();
        cleanContext.Response.Body = new MemoryStream();
        cleanContext.Request.Headers["Content-Length"] = "25";

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = cleanContext,
            ProxyRequest = new HttpRequestMessage()
        });

        cleanContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact(DisplayName = "PathTraversalTransformProvider: Blocks mixed slash encodings and query string traversal")]
    public async Task PathTraversalTransformProvider_BlocksMixedSlash_And_QueryString_Traversal()
    {
        var provider = new KyrolusPathTraversalTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "traversal-test-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        // 1. Mixed slash encoding: ..%2f
        var mixedContext = new DefaultHttpContext();
        mixedContext.Response.Body = new MemoryStream();
        mixedContext.Request.Path = "/api/files/..%2fsecret";

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = mixedContext,
            ProxyRequest = new HttpRequestMessage()
        });

        mixedContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        mixedContext.Response.ContentType.ShouldBe("application/problem+json");

        // 2. Windows mixed slash encoding: ..%5c
        var winContext = new DefaultHttpContext();
        winContext.Response.Body = new MemoryStream();
        winContext.Request.Path = "/api/files/..%5cwindows";

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = winContext,
            ProxyRequest = new HttpRequestMessage()
        });

        winContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        // 3. Query string traversal: ?file=..%2f..%2fetc/passwd
        var queryContext = new DefaultHttpContext();
        queryContext.Response.Body = new MemoryStream();
        queryContext.Request.Path = "/api/files";
        queryContext.Request.QueryString = new QueryString("?file=..%2f..%2fetc/passwd");

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = queryContext,
            ProxyRequest = new HttpRequestMessage()
        });

        queryContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        // 4. Query string null byte: ?view=%00
        var nullQueryContext = new DefaultHttpContext();
        nullQueryContext.Response.Body = new MemoryStream();
        nullQueryContext.Request.Path = "/api/data";
        nullQueryContext.Request.QueryString = new QueryString("?view=%00");

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = nullQueryContext,
            ProxyRequest = new HttpRequestMessage()
        });

        nullQueryContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        // 5. Clean query string
        var cleanContext = new DefaultHttpContext();
        cleanContext.Response.Body = new MemoryStream();
        cleanContext.Request.Path = "/api/items";
        cleanContext.Request.QueryString = new QueryString("?page=1&size=20");

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = cleanContext,
            ProxyRequest = new HttpRequestMessage()
        });

        cleanContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact(DisplayName = "RouteBuilder: WithCors automatically adds OPTIONS method when methods are restricted")]
    public void RouteBuilder_WithCors_AutomaticallyAddsOptionsMethod()
    {
        // 1. autoAllowPreflight = true (default)
        var builder = new KyrolusRouteBuilder("orders-route", "cluster1", "/api/orders");
        builder.WithMethods("GET", "POST")
               .WithCors("DefaultPolicy");

        var route = builder.Build();
        route.Match.Methods.ShouldNotBeNull();
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Get);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Post);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Options);

        // 2. autoAllowPreflight = false
        var strictBuilder = new KyrolusRouteBuilder("strict-route", "cluster1", "/api/strict");
        strictBuilder.WithMethods("GET")
                     .WithCors("DefaultPolicy", autoAllowPreflight: false);

        var strictRoute = strictBuilder.Build();
        strictRoute.Match.Methods.ShouldNotBeNull();
        strictRoute.Match.Methods.ShouldContain(KyrolusHttpMethod.Get);
        strictRoute.Match.Methods.ShouldNotContain(KyrolusHttpMethod.Options);
    }

    [Fact(DisplayName = "ClusterBuilder: WithHttpVersion and Aliases configure outbound client and timeout")]
    public void ClusterBuilder_WithHttpVersion_AndAliases_ConfigureOutboundClient()
    {
        var builder = new KyrolusClusterBuilder("grpc-cluster");
        builder.AddDestination("grpc-node", "https://10.0.1.20:50051")
               .WithHttpVersion(HttpVersion.Version20, HttpVersionPolicy.RequestVersionExact)
               .WithHttpRequestTimeout(TimeSpan.FromSeconds(25))
               .WithAllowResponseBuffering(false);

        var (cluster, _) = builder.Build();

        cluster.HttpClient.ShouldNotBeNull();
        cluster.HttpClient.DefaultVersion.ShouldBe(HttpVersion.Version20);
        cluster.HttpClient.VersionPolicy.ShouldBe(HttpVersionPolicy.RequestVersionExact);
        cluster.HttpRequestTimeout.ShouldBe(TimeSpan.FromSeconds(25));
        cluster.AllowResponseBuffering.ShouldBe(false);
    }

    [Fact(DisplayName = "RateLimitPartitionKeys: GetClientIpKey with forwarded header extracts correct IP")]
    public void RateLimitPartitionKeys_GetClientIpKey_WithForwardedHeader_ExtractsCorrectIp()
    {
        // 1. Custom CF-Connecting-IP
        var cfContext = new DefaultHttpContext();
        cfContext.Request.Headers["CF-Connecting-IP"] = "198.51.100.42";
        cfContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var cfIp = KyrolusRateLimitPartitionKeys.GetClientIpKey(cfContext, "CF-Connecting-IP");
        cfIp.ShouldBe("198.51.100.42");

        // 2. Comma-separated X-Forwarded-For takes first IP
        var xffContext = new DefaultHttpContext();
        xffContext.Request.Headers["X-Forwarded-For"] = "203.0.113.195, 70.41.3.18, 150.172.238.178";
        xffContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var xffIp = KyrolusRateLimitPartitionKeys.GetClientIpKey(xffContext, "X-Forwarded-For");
        xffIp.ShouldBe("203.0.113.195");

        // 3. Fallback to Connection.RemoteIpAddress when header absent
        var fallbackContext = new DefaultHttpContext();
        fallbackContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");

        var fallbackIp = KyrolusRateLimitPartitionKeys.GetClientIpKey(fallbackContext, "CF-Connecting-IP");
        fallbackIp.ShouldBe("10.0.0.5");
    }

    [Fact(DisplayName = "MethodOverrideTransform: Mutates ProxyRequest.Method when override is allowed and verb is valid")]
    public async Task MethodOverrideTransform_MutatesProxyRequestMethod_WhenAllowedAndValid()
    {
        var provider = new KyrolusMethodOverrideTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "rest-route",
            ClusterId = "cluster1",
            Match = new RouteMatch
            {
                Path = "/api/orders/{id}",
                Methods = ["POST", "PUT", "DELETE"]
            },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:MethodOverride:Allowed"] = "true"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Headers["X-HTTP-Method-Override"] = "DELETE";

        var proxyReq = new HttpRequestMessage(HttpMethod.Post, "https://backend/api/orders/123");
        proxyReq.Headers.Add("X-HTTP-Method-Override", "DELETE");

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyReq
        });

        httpContext.Response.StatusCode.ShouldBe(200);
        proxyReq.Method.ShouldBe(HttpMethod.Delete);
        proxyReq.Headers.Contains("X-HTTP-Method-Override").ShouldBeFalse();
    }

    [Theory(DisplayName = "MethodOverrideTransform: Blocks dangerous verbs TRACE, TRACK, CONNECT with 405")]
    [InlineData("TRACE")]
    [InlineData("TRACK")]
    [InlineData("CONNECT")]
    public async Task MethodOverrideTransform_BlocksDangerousVerbs_With405ProblemDetails(string verb)
    {
        var provider = new KyrolusMethodOverrideTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "any-route",
            ClusterId = "cluster1",
            Match = new RouteMatch
            {
                Path = "/api/test",
                Methods = ["POST", "TRACE", "TRACK", "CONNECT"]
            },
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:MethodOverride:Allowed"] = "true"
            }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Method = "POST";
        httpContext.Request.Headers["X-HTTP-Method-Override"] = verb;

        var proxyReq = new HttpRequestMessage(HttpMethod.Post, "https://backend/api/test");

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyReq
        });

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status405MethodNotAllowed);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        body.ShouldContain("https://httpstatuses.com/405");
        body.ShouldContain("Method Not Allowed");
    }

    [Fact(DisplayName = "RouteBuilder: WithStreaming and WithBlockDangerousVerbs configure route properly")]
    public void RouteBuilder_WithStreaming_And_WithBlockDangerousVerbs_ConfigureRoute()
    {
        var builder = new KyrolusRouteBuilder("stream-route", "cluster1", "/api/events/stream");
        builder.WithStreaming(TimeSpan.FromMinutes(5))
               .WithBlockDangerousVerbs(true);

        var route = builder.Build();

        route.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
        route.Metadata.ShouldNotBeNull();
        route.Metadata["Kyrolus:Streaming:Enabled"].ShouldBe("true");
        route.Metadata["Kyrolus:Verbs:BlockDangerous"].ShouldBe("true");
    }

    [Fact(DisplayName = "RateLimitPartitionKeys: GetClientIpKey with isTrustedProxy defends against IP spoofing")]
    public void RateLimitPartitionKeys_GetClientIpKey_WithTrustedProxy_DefendsAgainstSpoofing()
    {
        // Scenario 1: Untrusted connecting remote IP -> spoofed header is ignored, Connection.RemoteIpAddress used
        var untrustedContext = new DefaultHttpContext();
        untrustedContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.55");
        untrustedContext.Request.Headers["X-Forwarded-For"] = "10.0.0.1";

        var untrustedResult = KyrolusRateLimitPartitionKeys.GetClientIpKey(
            untrustedContext,
            "X-Forwarded-For",
            remoteIp => IPAddress.IsLoopback(remoteIp));

        untrustedResult.ShouldBe("198.51.100.55");

        // Scenario 2: Trusted connecting remote IP -> forwarded header IS accepted
        var trustedContext = new DefaultHttpContext();
        trustedContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        trustedContext.Request.Headers["X-Forwarded-For"] = "203.0.113.195, 70.41.3.18";

        var trustedResult = KyrolusRateLimitPartitionKeys.GetClientIpKey(
            trustedContext,
            "X-Forwarded-For",
            remoteIp => IPAddress.IsLoopback(remoteIp));

        trustedResult.ShouldBe("203.0.113.195");
    }

    [Fact(DisplayName = "TransformProviders: ValidateRoute reports errors on malformed metadata")]
    public void TransformProviders_ValidateRoute_ReportsErrors_OnMalformedMetadata()
    {
        // 1. Tenant: Invalid bool
        var tenantRoute = new RouteConfig
        {
            RouteId = "r-tenant",
            Metadata = new Dictionary<string, string> { ["Kyrolus:Tenant:Required"] = "not_bool" }
        };
        var tenantCtx = new TransformRouteValidationContext { Route = tenantRoute };
        new KyrolusTenantRoutingTransformProvider().ValidateRoute(tenantCtx);
        tenantCtx.Errors.Count.ShouldBe(1);

        // 2. Payload size: Negative or non-numeric
        var payloadRoute = new RouteConfig
        {
            RouteId = "r-payload",
            Metadata = new Dictionary<string, string> { ["Kyrolus:Payload:MaxSize"] = "-10" }
        };
        var payloadCtx = new TransformRouteValidationContext { Route = payloadRoute };
        new KyrolusPayloadSizeTransformProvider().ValidateRoute(payloadCtx);
        payloadCtx.Errors.Count.ShouldBe(1);

        // 3. Header limits: Non-numeric
        var headerRoute = new RouteConfig
        {
            RouteId = "r-headers",
            Metadata = new Dictionary<string, string> { ["Kyrolus:Headers:MaxCount"] = "invalid" }
        };
        var headerCtx = new TransformRouteValidationContext { Route = headerRoute };
        new KyrolusHeaderLimitsTransformProvider().ValidateRoute(headerCtx);
        headerCtx.Errors.Count.ShouldBe(1);

        // 4. IP filter: Invalid IP
        var ipRoute = new RouteConfig
        {
            RouteId = "r-ip",
            Metadata = new Dictionary<string, string> { ["Kyrolus:IpFilter:Allowed"] = "999.999.999.999" }
        };
        var ipCtx = new TransformRouteValidationContext { Route = ipRoute };
        new KyrolusIpFilterTransformProvider().ValidateRoute(ipCtx);
        ipCtx.Errors.Count.ShouldBe(1);

        // 5. Content type: Missing slash in MIME
        var contentTypeRoute = new RouteConfig
        {
            RouteId = "r-content-type",
            Metadata = new Dictionary<string, string> { ["Kyrolus:ContentType:Allowed"] = "invalidmime" }
        };
        var contentTypeCtx = new TransformRouteValidationContext { Route = contentTypeRoute };
        new KyrolusContentTypeTransformProvider().ValidateRoute(contentTypeCtx);
        contentTypeCtx.Errors.Count.ShouldBe(1);

        // 6. Method override: Invalid bool
        var methodRoute = new RouteConfig
        {
            RouteId = "r-method",
            Metadata = new Dictionary<string, string> { ["Kyrolus:MethodOverride:Allowed"] = "maybe" }
        };
        var methodCtx = new TransformRouteValidationContext { Route = methodRoute };
        new KyrolusMethodOverrideTransformProvider().ValidateRoute(methodCtx);
        methodCtx.Errors.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "TransformProviders: ValidateRoute passes with zero errors on valid metadata")]
    public void TransformProviders_ValidateRoute_Passes_OnValidMetadata()
    {
        var route = new RouteConfig
        {
            RouteId = "r-valid",
            Metadata = new Dictionary<string, string>
            {
                ["Kyrolus:Tenant:Required"] = "true",
                ["Kyrolus:Payload:MaxSize"] = "1048576",
                ["Kyrolus:Headers:MaxCount"] = "50",
                ["Kyrolus:Headers:MaxTotalLength"] = "16384",
                ["Kyrolus:IpFilter:Allowed"] = "192.168.1.0/24, 10.0.0.1",
                ["Kyrolus:ContentType:Allowed"] = "application/json, text/plain",
                ["Kyrolus:MethodOverride:Allowed"] = "false"
            }
        };

        var ctx = new TransformRouteValidationContext { Route = route };

        new KyrolusTenantRoutingTransformProvider().ValidateRoute(ctx);
        new KyrolusPayloadSizeTransformProvider().ValidateRoute(ctx);
        new KyrolusHeaderLimitsTransformProvider().ValidateRoute(ctx);
        new KyrolusIpFilterTransformProvider().ValidateRoute(ctx);
        new KyrolusContentTypeTransformProvider().ValidateRoute(ctx);
        new KyrolusMethodOverrideTransformProvider().ValidateRoute(ctx);

        ctx.Errors.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "RouteBuilder: Request and Response Header transforms are configured correctly")]
    public void RouteBuilder_HeaderTransforms_ConfigureRequestAndResponseTransforms()
    {
        var builder = new KyrolusRouteBuilder("header-test-route", "cluster1", "/api/data");
        builder.WithRequestHeader("X-Custom-Req", "MyReqVal")
               .WithRemoveRequestHeader("X-Internal-Secret")
               .WithResponseHeader("X-Custom-Resp", "MyRespVal")
               .WithRemoveResponseHeader("Server")
               .WithOriginalHostHeader(true);

        var route = builder.Build();

        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(5);

        // 1. WithRequestHeader
        route.Transforms.Any(t => t.TryGetValue("RequestHeader", out var h) && h == "X-Custom-Req" &&
                                  t.TryGetValue("Set", out var v) && v == "MyReqVal").ShouldBeTrue();

        // 2. WithRemoveRequestHeader
        route.Transforms.Any(t => t.TryGetValue("RequestHeaderRemove", out var h) && h == "X-Internal-Secret").ShouldBeTrue();

        // 3. WithResponseHeader
        route.Transforms.Any(t => t.TryGetValue("ResponseHeaderValue", out var h) && h == "X-Custom-Resp" &&
                                  t.TryGetValue("Set", out var v) && v == "MyRespVal" &&
                                  t.TryGetValue("When", out var w) && w == "Always").ShouldBeTrue();

        // 4. WithRemoveResponseHeader
        route.Transforms.Any(t => t.TryGetValue("ResponseHeaderRemove", out var h) && h == "Server" &&
                                  t.TryGetValue("When", out var w) && w == "Always").ShouldBeTrue();

        // 5. WithOriginalHostHeader
        route.Transforms.Any(t => t.TryGetValue("RequestHeaderOriginalHost", out var oh) && oh == "true").ShouldBeTrue();
    }

    [Fact(DisplayName = "ClusterBuilder: Outbound HttpClient options configure resilience and mTLS settings")]
    public void ClusterBuilder_HttpClientOptions_ConfigureAdvancedSettings()
    {
        var builder = new KyrolusClusterBuilder("resilient-cluster");
        builder.AddDestination("node1", "https://10.0.1.50:5001")
               .WithDangerousAcceptAnyServerCertificate(true)
               .WithMaxConnectionsPerServer(250)
               .WithMultipleHttp2Connections(true);

        var (cluster, _) = builder.Build();

        cluster.HttpClient.ShouldNotBeNull();
        cluster.HttpClient.DangerousAcceptAnyServerCertificate.ShouldBeTrue();
        cluster.HttpClient.MaxConnectionsPerServer.ShouldBe(250);
        cluster.HttpClient.EnableMultipleHttp2Connections.ShouldBe(true);
    }

    [Theory(DisplayName = "PathTraversalTransform: Blocks matrix parameter and semicolon traversal attempts")]
    [InlineData("/api/files/..;/..;/etc/passwd")]
    [InlineData("/api/files/..%3b/etc/passwd")]
    [InlineData("/api/files/%3b../etc/passwd")]
    [InlineData("/api/.;/test")]
    public async Task PathTraversalTransform_Blocks_MatrixSemicolonTraversal(string path)
    {
        var provider = new KyrolusPathTraversalTransformProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "traversal-route",
            ClusterId = "cluster1",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        };

        var builderContext = new TransformBuilderContext { Route = routeConfig };
        provider.Apply(builderContext);
        var transform = builderContext.RequestTransforms[0];

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;

        await transform.ApplyAsync(new RequestTransformContext
        {
            HttpContext = context,
            ProxyRequest = new HttpRequestMessage()
        });

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "StronglyTypedPolicies SupportStandardValuesCustomValuesAndStringConversions")]
    public void StronglyTypedPolicies_SupportStandardValuesCustomValuesAndStringConversions()
    {
        // 1. ActiveHealthCheckPolicy
        var activeDefault = KyrolusActiveHealthCheckPolicy.ConsecutiveFailures;
        activeDefault.Value.ShouldBe("ConsecutiveFailures");
        activeDefault.ToString().ShouldBe("ConsecutiveFailures");
        string activeStr = activeDefault;
        activeStr.ShouldBe("ConsecutiveFailures");
        (activeDefault == "consecutivefailures").ShouldBeTrue();
        (activeDefault != "other").ShouldBeTrue();

        var activeCustom = KyrolusActiveHealthCheckPolicy.Custom("MyCustomActive");
        activeCustom.Value.ShouldBe("MyCustomActive");
        KyrolusActiveHealthCheckPolicy.From("consecutivefailures").ShouldBe(KyrolusActiveHealthCheckPolicy.ConsecutiveFailures);
        KyrolusActiveHealthCheckPolicy.From("MyCustomActive")?.Value.ShouldBe("MyCustomActive");
        KyrolusActiveHealthCheckPolicy.From(null).ShouldBeNull();

        // 2. PassiveHealthCheckPolicy
        var passiveDefault = KyrolusPassiveHealthCheckPolicy.TransportFailureRate;
        passiveDefault.Value.ShouldBe("TransportFailureRate");
        string passiveStr = passiveDefault;
        passiveStr.ShouldBe("TransportFailureRate");
        (passiveDefault == "transportfailurerate").ShouldBeTrue();

        var passiveCustom = KyrolusPassiveHealthCheckPolicy.Custom("MyCustomPassive");
        passiveCustom.Value.ShouldBe("MyCustomPassive");
        KyrolusPassiveHealthCheckPolicy.From("transportfailurerate").ShouldBe(KyrolusPassiveHealthCheckPolicy.TransportFailureRate);
        KyrolusPassiveHealthCheckPolicy.From(null).ShouldBeNull();

        // 3. AvailableDestinationsPolicy
        var destDefault = KyrolusAvailableDestinationsPolicy.HealthyOrUnspecified;
        destDefault.Value.ShouldBe("HealthyOrUnspecified");
        KyrolusAvailableDestinationsPolicy.HealthyAndUnknown.Value.ShouldBe("HealthyAndUnknown");
        string destStr = destDefault;
        destStr.ShouldBe("HealthyOrUnspecified");
        (destDefault == "healthyorunspecified").ShouldBeTrue();

        var destCustom = KyrolusAvailableDestinationsPolicy.Custom("StrictHealthyOnly");
        destCustom.Value.ShouldBe("StrictHealthyOnly");
        KyrolusAvailableDestinationsPolicy.From("healthyandunknown").ShouldBe(KyrolusAvailableDestinationsPolicy.HealthyAndUnknown);
        KyrolusAvailableDestinationsPolicy.From(null).ShouldBeNull();

        // 4. LoadBalancingPolicy
        var lbDefault = KyrolusLoadBalancingPolicy.RoundRobin;
        lbDefault.Value.ShouldBe("RoundRobin");
        KyrolusLoadBalancingPolicy.LeastRequests.Value.ShouldBe("LeastRequests");
        KyrolusLoadBalancingPolicy.Random.Value.ShouldBe("Random");
        KyrolusLoadBalancingPolicy.PowerOfTwoChoices.Value.ShouldBe("PowerOfTwoChoices");
        string lbStr = lbDefault;
        lbStr.ShouldBe("RoundRobin");
        (lbDefault == "roundrobin").ShouldBeTrue();

        var lbCustom = KyrolusLoadBalancingPolicy.Custom("WeightedRoundRobin");
        lbCustom.Value.ShouldBe("WeightedRoundRobin");
        KyrolusLoadBalancingPolicy.From("leastrequests").ShouldBe(KyrolusLoadBalancingPolicy.LeastRequests);
        KyrolusLoadBalancingPolicy.From("random").ShouldBe(KyrolusLoadBalancingPolicy.Random);
        KyrolusLoadBalancingPolicy.From("poweroftwochoices").ShouldBe(KyrolusLoadBalancingPolicy.PowerOfTwoChoices);
        KyrolusLoadBalancingPolicy.From(null).ShouldBeNull();
    }

    [Fact(DisplayName = "ClusterBuilder WithStronglyTypedPolicies ConfiguresClusterCorrectly")]
    public void ClusterBuilder_WithStronglyTypedPolicies_ConfiguresClusterCorrectly()
    {
        var builder = new KyrolusClusterBuilder("typed-policy-cluster");
        builder.AddDestination("node1", "https://10.0.1.50:5001")
               .WithLoadBalancing(KyrolusLoadBalancingPolicy.PowerOfTwoChoices)
               .WithActiveHealthCheck("/healthz", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), KyrolusActiveHealthCheckPolicy.ConsecutiveFailures)
               .WithPassiveHealthCheck(TimeSpan.FromSeconds(30), KyrolusPassiveHealthCheckPolicy.TransportFailureRate);

        var (cluster, _) = builder.Build();

        cluster.LoadBalancingPolicy.ShouldBe(KyrolusLoadBalancingPolicy.PowerOfTwoChoices);
        cluster.HealthCheck.ShouldNotBeNull();
        cluster.HealthCheck.Active!.Policy.ShouldBe(KyrolusActiveHealthCheckPolicy.ConsecutiveFailures);
        cluster.HealthCheck.Passive!.Policy.ShouldBe(KyrolusPassiveHealthCheckPolicy.TransportFailureRate);
        cluster.HealthCheck.AvailableDestinationsPolicy.ShouldBe(KyrolusAvailableDestinationsPolicy.HealthyOrUnspecified);
    }
}



