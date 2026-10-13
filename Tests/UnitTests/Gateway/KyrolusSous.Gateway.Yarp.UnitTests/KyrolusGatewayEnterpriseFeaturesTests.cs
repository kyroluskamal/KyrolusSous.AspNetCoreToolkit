namespace KyrolusSous.Gateway.Yarp.UnitTests;

using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using global::Yarp.ReverseProxy.Configuration;
using global::Yarp.ReverseProxy.Model;
using global::Yarp.ReverseProxy.Transforms;
using global::Yarp.ReverseProxy.Transforms.Builder;
using KyrolusSous.Gateway.Abstractions;
using KyrolusSous.Gateway.Yarp.Caching;
using KyrolusSous.Gateway.Yarp.Configuration;
using KyrolusSous.Gateway.Yarp.Diagnostics;
using KyrolusSous.Gateway.Yarp.Resilience;
using KyrolusSous.Gateway.Yarp.Transforms;

public sealed class KyrolusGatewayEnterpriseFeaturesTests
{
    // =========================================================================
    // 1. Maintenance Mode & Mock Response Tests
    // =========================================================================

    [Fact(DisplayName = "Maintenance Mode: Short-circuits with 503 ProblemDetails and Retry-After header")]
    public async Task MaintenanceMode_WhenEnabled_ShortCircuitsWith503AndProblemDetails()
    {
        var provider = new KyrolusMockAndMaintenanceTransformProvider();
        var builderContext = new TransformBuilderContext
        {
            Route = new RouteConfig
            {
                RouteId = "maintenance-route",
                ClusterId = "orders-cluster",
                Match = new RouteMatch { Path = "/api/orders" },
                Metadata = new Dictionary<string, string>
                {
                    [KyrolusGatewayMetadataKeys.Maintenance.Enabled] = "true",
                    [KyrolusGatewayMetadataKeys.Maintenance.StatusCode] = "503",
                    [KyrolusGatewayMetadataKeys.Maintenance.Message] = "Upgrading orders database.",
                    [KyrolusGatewayMetadataKeys.Maintenance.RetryAfterSeconds] = "120"
                }
            }
        };

        provider.Apply(builderContext);
        builderContext.RequestTransforms.Count.ShouldBe(1);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var transformCtx = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await builderContext.RequestTransforms[0].ApplyAsync(transformCtx);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");
        httpContext.Response.Headers["Retry-After"].ToString().ShouldBe("120");

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldContain("Upgrading orders database.");
        body.ShouldContain("503");
    }

    [Fact(DisplayName = "Mock Response: Short-circuits with static mock JSON and configured status code")]
    public async Task MockResponse_WhenEnabled_ReturnsStaticMockJson()
    {
        var provider = new KyrolusMockAndMaintenanceTransformProvider();
        var mockJson = """{"mock":true,"userId":42,"status":"Active"}""";

        var builderContext = new TransformBuilderContext
        {
            Route = new RouteConfig
            {
                RouteId = "mock-route",
                ClusterId = "users-cluster",
                Match = new RouteMatch { Path = "/api/users/42" },
                Metadata = new Dictionary<string, string>
                {
                    [KyrolusGatewayMetadataKeys.Mock.Enabled] = "true",
                    [KyrolusGatewayMetadataKeys.Mock.StatusCode] = "200",
                    [KyrolusGatewayMetadataKeys.Mock.ResponseBody] = mockJson,
                    [KyrolusGatewayMetadataKeys.Mock.ContentType] = "application/json"
                }
            }
        };

        provider.Apply(builderContext);
        builderContext.RequestTransforms.Count.ShouldBe(1);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var transformCtx = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await builderContext.RequestTransforms[0].ApplyAsync(transformCtx);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        httpContext.Response.ContentType.ShouldBe("application/json");

        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldBe(mockJson);
    }

    [Fact(DisplayName = "RouteBuilder: Fluent methods configure Maintenance and Mock metadata accurately")]
    public void RouteBuilder_ConfiguresMaintenanceAndMock_Accurately()
    {
        var route1 = new KyrolusRouteBuilder("maint-route", "cluster1", "/maint")
            .WithMaintenanceMode(503, "System down", 300)
            .Build();

        route1.Metadata.ShouldNotBeNull();
        route1.Metadata[KyrolusGatewayMetadataKeys.Maintenance.Enabled].ShouldBe("true");
        route1.Metadata[KyrolusGatewayMetadataKeys.Maintenance.StatusCode].ShouldBe("503");
        route1.Metadata[KyrolusGatewayMetadataKeys.Maintenance.Message].ShouldBe("System down");
        route1.Metadata[KyrolusGatewayMetadataKeys.Maintenance.RetryAfterSeconds].ShouldBe("300");

        var route2 = new KyrolusRouteBuilder("mock-route", "cluster2", "/mock")
            .WithMockResponse("""{"ping":"pong"}""", 200, "application/json")
            .Build();

        route2.Metadata.ShouldNotBeNull();
        route2.Metadata[KyrolusGatewayMetadataKeys.Mock.Enabled].ShouldBe("true");
        route2.Metadata[KyrolusGatewayMetadataKeys.Mock.StatusCode].ShouldBe("200");
        route2.Metadata[KyrolusGatewayMetadataKeys.Mock.ResponseBody].ShouldBe("""{"ping":"pong"}""");
        route2.Metadata[KyrolusGatewayMetadataKeys.Mock.ContentType].ShouldBe("application/json");
    }

    // =========================================================================
    // 2. Edge API Key Authentication Tests
    // =========================================================================

    [Fact(DisplayName = "Edge API Key: Rejects missing API key with 401 ProblemDetails")]
    public async Task ApiKey_MissingKey_RejectsWith401()
    {
        var provider = new KyrolusApiKeyTransformProvider();
        var builderContext = new TransformBuilderContext
        {
            Route = new RouteConfig
            {
                RouteId = "secure-api",
                ClusterId = "secure-cluster",
                Match = new RouteMatch { Path = "/api/v1/secure" },
                Metadata = new Dictionary<string, string>
                {
                    [KyrolusGatewayMetadataKeys.ApiKey.Required] = "true",
                    [KyrolusGatewayMetadataKeys.ApiKey.HeaderName] = "X-Api-Key",
                    [KyrolusGatewayMetadataKeys.ApiKey.AllowedKeys] = "secret-key-1,secret-key-2"
                }
            }
        };

        provider.Apply(builderContext);
        builderContext.RequestTransforms.Count.ShouldBe(1);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var transformCtx = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await builderContext.RequestTransforms[0].ApplyAsync(transformCtx);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        httpContext.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "Edge API Key: Valid static key in header allows request to proceed")]
    public async Task ApiKey_ValidStaticKeyInHeader_AllowsRequest()
    {
        var provider = new KyrolusApiKeyTransformProvider();
        var builderContext = new TransformBuilderContext
        {
            Route = new RouteConfig
            {
                RouteId = "secure-api",
                ClusterId = "secure-cluster",
                Match = new RouteMatch { Path = "/api/v1/secure" },
                Metadata = new Dictionary<string, string>
                {
                    [KyrolusGatewayMetadataKeys.ApiKey.Required] = "true",
                    [KyrolusGatewayMetadataKeys.ApiKey.HeaderName] = "X-Api-Key",
                    [KyrolusGatewayMetadataKeys.ApiKey.AllowedKeys] = "secret-key-1,secret-key-2"
                }
            }
        };

        provider.Apply(builderContext);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Api-Key"] = "secret-key-1";

        var transformCtx = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await builderContext.RequestTransforms[0].ApplyAsync(transformCtx);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK); // Untouched 200 default
    }

    [Fact(DisplayName = "Edge API Key: Valid key in query string fallback allows request to proceed")]
    public async Task ApiKey_ValidKeyInQueryString_AllowsRequest()
    {
        var provider = new KyrolusApiKeyTransformProvider();
        var builderContext = new TransformBuilderContext
        {
            Route = new RouteConfig
            {
                RouteId = "secure-api",
                ClusterId = "secure-cluster",
                Match = new RouteMatch { Path = "/api/v1/secure" },
                Metadata = new Dictionary<string, string>
                {
                    [KyrolusGatewayMetadataKeys.ApiKey.Required] = "true",
                    [KyrolusGatewayMetadataKeys.ApiKey.AllowedKeys] = "allowed-query-key"
                }
            }
        };

        provider.Apply(builderContext);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?apiKey=allowed-query-key");

        var transformCtx = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage()
        };

        await builderContext.RequestTransforms[0].ApplyAsync(transformCtx);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "Edge API Key: Dynamic IKyrolusApiKeyValidator in DI accepts valid keys and rejects invalid")]
    public async Task ApiKey_DynamicValidator_ValidatesProperly()
    {
        var provider = new KyrolusApiKeyTransformProvider();
        var builderContext = new TransformBuilderContext
        {
            Route = new RouteConfig
            {
                RouteId = "dynamic-api",
                ClusterId = "dynamic-cluster",
                Match = new RouteMatch { Path = "/dynamic" },
                Metadata = new Dictionary<string, string>
                {
                    [KyrolusGatewayMetadataKeys.ApiKey.Required] = "true"
                }
            }
        };

        provider.Apply(builderContext);

        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusApiKeyValidator, TestApiKeyValidator>();
        var sp = services.BuildServiceProvider();

        // 1. Valid dynamic key
        var validContext = new DefaultHttpContext { RequestServices = sp };
        validContext.Request.Headers["X-Api-Key"] = "dyn-valid-123";
        await builderContext.RequestTransforms[0].ApplyAsync(new RequestTransformContext
        {
            HttpContext = validContext,
            ProxyRequest = new HttpRequestMessage()
        });
        validContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        // 2. Invalid dynamic key
        var invalidContext = new DefaultHttpContext { RequestServices = sp };
        invalidContext.Response.Body = new MemoryStream();
        invalidContext.Request.Headers["X-Api-Key"] = "bad-key";
        await builderContext.RequestTransforms[0].ApplyAsync(new RequestTransformContext
        {
            HttpContext = invalidContext,
            ProxyRequest = new HttpRequestMessage()
        });
        invalidContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    private sealed class TestApiKeyValidator : IKyrolusApiKeyValidator
    {
        public ValueTask<bool> ValidateAsync(string apiKey, HttpContext context, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(apiKey == "dyn-valid-123");
        }
    }

    // =========================================================================
    // 3. Weighted Load Balancing & Traffic Splitting Tests
    // =========================================================================

    [Fact(DisplayName = "Weighted Load Balancing: Distributes traffic proportionally across destinations")]
    public void WeightedLoadBalancing_DistributesTraffic_Proportionally()
    {
        var policy = new KyrolusWeightedLoadBalancingPolicy();
        policy.Name.ShouldBe(KyrolusLoadBalancingPolicies.Weighted);

        var d1Config = new DestinationConfig
        {
            Address = "http://srv1:5000",
            Metadata = new Dictionary<string, string> { [KyrolusGatewayMetadataKeys.Weighted.Weight] = "80" }
        };
        var d2Config = new DestinationConfig
        {
            Address = "http://srv2:5000",
            Metadata = new Dictionary<string, string> { [KyrolusGatewayMetadataKeys.Weighted.Weight] = "20" }
        };

        var d1 = CreateDestinationState("srv1", d1Config);
        var d2 = CreateDestinationState("srv2", d2Config);
        var cluster = new ClusterState("test-cluster");

        var destinations = new List<DestinationState> { d1, d2 };
        var context = new DefaultHttpContext();

        var counts = new Dictionary<string, int> { ["srv1"] = 0, ["srv2"] = 0 };
        const int iterations = 1000;

        for (var i = 0; i < iterations; i++)
        {
            var picked = policy.PickDestination(context, cluster, destinations);
            picked.ShouldNotBeNull();
            counts[picked.DestinationId]++;
        }

        // 80/20 distribution with reasonable statistical margin
        counts["srv1"].ShouldBeGreaterThan(650);
        counts["srv2"].ShouldBeGreaterThan(100);
        (counts["srv1"] + counts["srv2"]).ShouldBe(iterations);
    }

    [Fact(DisplayName = "Weighted Load Balancing: Canary destination header override routes directly")]
    public void WeightedLoadBalancing_CanaryHeader_DirectsToTarget()
    {
        var policy = new KyrolusWeightedLoadBalancingPolicy();

        var d1 = CreateDestinationState("primary", new DestinationConfig { Address = "http://primary:5000" });
        var d2 = CreateDestinationState("canary-v2", new DestinationConfig { Address = "http://canary:5000" });
        var cluster = new ClusterState("test-cluster");

        var destinations = new List<DestinationState> { d1, d2 };
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Canary-Destination"] = "canary-v2";

        var picked = policy.PickDestination(context, cluster, destinations);
        picked.ShouldNotBeNull();
        picked.DestinationId.ShouldBe("canary-v2");
    }

    [Fact(DisplayName = "ClusterBuilder: Configures weighted destinations and weighted policy fluently")]
    public void ClusterBuilder_ConfiguresWeightedDestinations_Fluently()
    {
        var builder = new KyrolusClusterBuilder("payments-cluster")
            .WithWeightedLoadBalancing()
            .AddDestination("node1", "http://node1:5000", 90)
            .AddDestination("node2", "http://node2:5000", 10);

        var (cluster, _) = builder.Build();

        cluster.LoadBalancingPolicy.ShouldBe(KyrolusLoadBalancingPolicy.Weighted);
        cluster.Destinations["node1"].Weight.ShouldBe(90);
        cluster.Destinations["node2"].Weight.ShouldBe(10);
    }

    // =========================================================================
    // 4. Edge Response Caching Tests
    // =========================================================================

    [Fact(DisplayName = "Edge Response Caching: Caches GET response and serves subsequent request with X-Kyrolus-Cache: HIT")]
    public async Task EdgeCache_CachesResponse_AndServesHit()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache>(memoryCache);
        var sp = services.BuildServiceProvider();

        var routeConfig = new RouteConfig
        {
            RouteId = "cached-route",
            ClusterId = "cached-cluster",
            Metadata = new Dictionary<string, string>
            {
                [KyrolusGatewayMetadataKeys.Cache.Enabled] = "true",
                [KyrolusGatewayMetadataKeys.Cache.DurationSeconds] = "60"
            }
        };

        var executedCount = 0;
        RequestDelegate next = ctx =>
        {
            executedCount++;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("""{"data":"cached-result"}""");
        };

        var middleware = new KyrolusEdgeCacheMiddleware(next, sp);

        // 1. First Request -> MISS
        var ctx1 = new DefaultHttpContext { RequestServices = sp };
        ctx1.Request.Method = "GET";
        ctx1.Request.Path = "/api/products";
        ctx1.Features.Set<IReverseProxyFeature>(CreateProxyFeature(routeConfig));

        await middleware.InvokeAsync(ctx1);

        ctx1.Response.Headers["X-Kyrolus-Cache"].ToString().ShouldBe("MISS");
        executedCount.ShouldBe(1);

        // 2. Second Request -> HIT
        var ctx2 = new DefaultHttpContext { RequestServices = sp };
        ctx2.Request.Method = "GET";
        ctx2.Request.Path = "/api/products";
        ctx2.Response.Body = new MemoryStream();
        ctx2.Features.Set<IReverseProxyFeature>(CreateProxyFeature(routeConfig));

        await middleware.InvokeAsync(ctx2);

        ctx2.Response.Headers["X-Kyrolus-Cache"].ToString().ShouldBe("HIT");
        executedCount.ShouldBe(1); // Pipeline was not invoked again!

        ctx2.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx2.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldBe("""{"data":"cached-result"}""");
    }

    [Fact(DisplayName = "Edge Response Caching: POST mutations bypass cache completely")]
    public async Task EdgeCache_PostMethod_BypassesCache()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var routeConfig = new RouteConfig
        {
            RouteId = "mutate-route",
            Metadata = new Dictionary<string, string>
            {
                [KyrolusGatewayMetadataKeys.Cache.Enabled] = "true"
            }
        };

        var executed = false;
        RequestDelegate next = ctx =>
        {
            executed = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusEdgeCacheMiddleware(next, sp);
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/api/products";
        ctx.Features.Set<IReverseProxyFeature>(CreateProxyFeature(routeConfig));

        await middleware.InvokeAsync(ctx);

        executed.ShouldBeTrue();
        ctx.Response.Headers.ContainsKey("X-Kyrolus-Cache").ShouldBeFalse();
    }

    // =========================================================================
    // 5. Smart Retry & Failover Tests
    // =========================================================================

    [Fact(DisplayName = "Smart Retry: Idempotent GET request retries on 503 and returns successful recovery")]
    public async Task FailoverHandler_IdempotentGet_RetriesOn503AndSucceeds()
    {
        var attempt = 0;
        var innerHandler = new MockHttpMessageHandler((req, ct) =>
        {
            attempt++;
            if (attempt == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Recovered successfully!")
            };
        });

        using var retryHandler = new KyrolusFailoverHttpMessageHandler(
            maxRetries: 2,
            backoffMs: 1, // minimal for fast test
            retryableStatusCodes: [502, 503, 504],
            idempotentOnly: true)
        {
            InnerHandler = innerHandler
        };

        using var invoker = new HttpMessageInvoker(retryHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend/api/items");

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        attempt.ShouldBe(2);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("Recovered successfully!");
    }

    [Fact(DisplayName = "Smart Retry: Non-idempotent POST request is never retried to prevent duplicate mutation")]
    public async Task FailoverHandler_NonIdempotentPost_NeverRetries()
    {
        var attempt = 0;
        var innerHandler = new MockHttpMessageHandler((req, ct) =>
        {
            attempt++;
            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        });

        using var retryHandler = new KyrolusFailoverHttpMessageHandler(
            maxRetries: 3,
            backoffMs: 1,
            retryableStatusCodes: [502, 503],
            idempotentOnly: true)
        {
            InnerHandler = innerHandler
        };

        using var invoker = new HttpMessageInvoker(retryHandler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://backend/api/orders");

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        attempt.ShouldBe(1); // Crucial: executed only ONCE!
    }

    // =========================================================================
    // 6. Access Logging & Observability Middleware Tests
    // =========================================================================

    [Fact(DisplayName = "Access Logging Middleware: Captures execution and invokes pipeline cleanly")]
    public async Task AccessLoggingMiddleware_ExecutesCleanly()
    {
        var executed = false;
        RequestDelegate next = ctx =>
        {
            executed = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusGatewayAccessLoggingMiddleware(next, NullLogger<KyrolusGatewayAccessLoggingMiddleware>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/test";

        await middleware.InvokeAsync(httpContext);

        executed.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    // Helper methods for testing
    private static IReverseProxyFeature CreateProxyFeature(RouteConfig routeConfig)
    {
        var feature = Substitute.For<IReverseProxyFeature>();
        var cluster = new ClusterState(routeConfig.ClusterId ?? "test-cluster");
        var routeModel = new RouteModel(routeConfig, cluster, global::Yarp.ReverseProxy.Forwarder.HttpTransformer.Empty);
        feature.Route.Returns(routeModel);
        return feature;
    }

    private static DestinationState CreateDestinationState(string id, DestinationConfig config)
    {
        var state = new DestinationState(id);
        var model = new DestinationModel(config);
        var field = typeof(DestinationState).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(f => f.Name.Contains("model", StringComparison.OrdinalIgnoreCase) || f.FieldType == typeof(DestinationModel));

        if (field != null)
        {
            field.SetValue(state, model);
        }
        else
        {
            var prop = typeof(DestinationState).GetProperty("Model");
            prop?.SetValue(state, model);
        }

        return state;
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request, cancellationToken));
        }
    }
}
