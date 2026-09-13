using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class RoutingPartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Route, Method, and Method Safety resolve accurately")]
    public void Route_Method_And_MethodSafety_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;
        context.Request.Path = "/api/orders/pay";
        context.Request.Method = "POST";

        KyrolusRateLimitPartitionKeys.GetRouteAndClientKey(context).ShouldBe("/api/orders/pay:198.51.100.42");
        KyrolusRateLimitPartitionKeys.GetMethodAndClientKey(context).ShouldBe("POST:198.51.100.42");

        // Method Safety
        KyrolusRateLimitPartitionKeys.GetMethodSafetyBucketKey(context).ShouldBe("method_mutation");
        KyrolusRateLimitPartitionKeys.GetMethodSafetyBucketAndClientKey(context).ShouldBe("method_mutation:198.51.100.42");

        context.Request.Method = "GET";
        KyrolusRateLimitPartitionKeys.GetMethodSafetyBucketKey(context).ShouldBe("method_safe");

        context.Request.Method = "PUT";
        KyrolusRateLimitPartitionKeys.GetMethodSafetyBucketKey(context).ShouldBe("method_idempotent");
    }

    [Fact(DisplayName = "Query, Query Complexity, and Custom Headers resolve accurately")]
    public void Query_QueryComplexity_And_CustomHeaders_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Query
        KyrolusRateLimitPartitionKeys.GetQueryKey(context, "client_id").ShouldBe("ip_198.51.100.42");
        context.Request.QueryString = new QueryString("?client_id=acme_corp_007");
        KyrolusRateLimitPartitionKeys.GetQueryKey(context, "client_id").ShouldBe("client_id_acme_corp_007");

        // Query Complexity
        var complexQuery = new DefaultHttpContext();
        complexQuery.Connection.RemoteIpAddress = BaseIp;
        complexQuery.Request.QueryString = new QueryString("?a=1&b=2&c=3&d=4&e=5");
        KyrolusRateLimitPartitionKeys.GetQueryComplexityBucketKey(complexQuery, maxSimpleQueryCount: 3).ShouldBe("query_complex");
        KyrolusRateLimitPartitionKeys.GetQueryComplexityBucketAndClientKey(complexQuery, maxSimpleQueryCount: 3).ShouldBe("query_complex:198.51.100.42");

        // Custom Header
        var headerContext = new DefaultHttpContext();
        headerContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetCustomHeaderKey(headerContext, "X-Device-ID").ShouldBe("ip_198.51.100.42");
        headerContext.Request.Headers["X-Device-ID"] = "iphone_xyz_789";
        KyrolusRateLimitPartitionKeys.GetCustomHeaderKey(headerContext, "X-Device-ID").ShouldBe("x-device-id_iphone_xyz_789");
    }

    [Fact(DisplayName = "Session Cookie, Host, and Cluster resolve accurately")]
    public void SessionCookie_Host_And_Cluster_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Session Cookie
        KyrolusRateLimitPartitionKeys.GetSessionCookieKey(context).ShouldBe("ip_198.51.100.42");
        context.Request.Headers.Cookie = ".AspNetCore.Session=sess_secret_token_123; other=abc";
        KyrolusRateLimitPartitionKeys.GetSessionCookieKey(context).ShouldBe("cookie_sess_secret_token_123");

        // Host
        var hostContext = new DefaultHttpContext();
        hostContext.Connection.RemoteIpAddress = BaseIp;
        hostContext.Request.Host = new HostString("api.acme.com", 8080);
        KyrolusRateLimitPartitionKeys.GetHostKey(hostContext).ShouldBe("host_api.acme.com");
        KyrolusRateLimitPartitionKeys.GetHostKey(hostContext, includePort: true).ShouldBe("host_api.acme.com:8080");
        KyrolusRateLimitPartitionKeys.GetHostAndClientKey(hostContext).ShouldBe("host_api.acme.com:198.51.100.42");

        // Cluster
        var clusterContext = new DefaultHttpContext();
        clusterContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetClusterAndClientKey(clusterContext).ShouldBe("cluster_unknown_cluster:198.51.100.42");

        var routeConfig = new RouteConfig { RouteId = "orders-route", ClusterId = "payments-cluster" };
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(routeConfig),
            "Orders Endpoint");
        clusterContext.SetEndpoint(endpoint);
        KyrolusRateLimitPartitionKeys.GetClusterAndClientKey(clusterContext).ShouldBe("cluster_payments-cluster:198.51.100.42");
    }

    [Fact(DisplayName = "Origin, Referer, ApiVersion, and Locale resolve accurately")]
    public void Origin_Referer_ApiVersion_And_Locale_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Origin
        KyrolusRateLimitPartitionKeys.GetOriginKey(context).ShouldBe("origin_unknown");
        context.Request.Headers.Origin = "https://app.kyrolus.com";
        KyrolusRateLimitPartitionKeys.GetOriginKey(context).ShouldBe("origin_app.kyrolus.com");
        KyrolusRateLimitPartitionKeys.GetOriginAndClientKey(context).ShouldBe("origin_app.kyrolus.com:198.51.100.42");

        // Referer
        var refContext = new DefaultHttpContext();
        refContext.Connection.RemoteIpAddress = BaseIp;
        KyrolusRateLimitPartitionKeys.GetRefererKey(refContext).ShouldBe("ref_direct");
        refContext.Request.Headers.Referer = "https://checkout.acme.org/pay";
        KyrolusRateLimitPartitionKeys.GetRefererKey(refContext).ShouldBe("ref_checkout.acme.org");

        // ApiVersion
        var verContext = new DefaultHttpContext();
        verContext.Connection.RemoteIpAddress = BaseIp;
        verContext.Request.Headers["X-Version"] = "2.1";
        KyrolusRateLimitPartitionKeys.GetApiVersionKey(verContext).ShouldBe("version_2.1");

        // Locale
        var locContext = new DefaultHttpContext();
        locContext.Connection.RemoteIpAddress = BaseIp;
        locContext.Request.Headers.AcceptLanguage = "en-US,en;q=0.9,ar;q=0.8";
        KyrolusRateLimitPartitionKeys.GetLocaleKey(locContext).ShouldBe("lang_en");
    }

    [Fact(DisplayName = "Endpoint SLA, API Deprecation, and Tenant Vanity Subdomain resolve accurately")]
    public void EndpointTier_Deprecation_And_VanitySubdomain_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // SLA Endpoint Tier
        var routeConfig = new RouteConfig
        {
            RouteId = "payment-route",
            Metadata = new Dictionary<string, string> { ["tier"] = "critical" }
        };
        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(routeConfig), "PaymentEndpoint");
        context.SetEndpoint(endpoint);
        KyrolusRateLimitPartitionKeys.GetEndpointTierKey(context).ShouldBe("sla_critical");
        KyrolusRateLimitPartitionKeys.GetEndpointTierAndClientKey(context).ShouldBe("sla_critical:198.51.100.42");

        // API Deprecation
        var depContext = new DefaultHttpContext();
        depContext.Connection.RemoteIpAddress = BaseIp;
        depContext.Request.Headers["X-Api-Sunset"] = "true";
        KyrolusRateLimitPartitionKeys.GetApiDeprecationStateKey(depContext).ShouldBe("api_sunset");
        KyrolusRateLimitPartitionKeys.GetApiDeprecationStateAndClientKey(depContext).ShouldBe("api_sunset:198.51.100.42");

        // Tenant Vanity Subdomain
        var vanityContext = new DefaultHttpContext();
        vanityContext.Connection.RemoteIpAddress = BaseIp;
        vanityContext.Request.Host = new HostString("customer1.api.kyrolus.com");
        KyrolusRateLimitPartitionKeys.GetTenantSubdomainTierKey(vanityContext, "kyrolus.com").ShouldBe("tenant_vanity_subdomain");
        KyrolusRateLimitPartitionKeys.GetTenantSubdomainTierAndClientKey(vanityContext, "kyrolus.com").ShouldBe("tenant_vanity_subdomain:198.51.100.42");

        var apexContext = new DefaultHttpContext();
        apexContext.Request.Host = new HostString("kyrolus.com");
        KyrolusRateLimitPartitionKeys.GetTenantSubdomainTierKey(apexContext, "kyrolus.com").ShouldBe("tenant_shared_apex");
    }
}
