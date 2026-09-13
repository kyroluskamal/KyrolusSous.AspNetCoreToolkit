using System.Net;
using KyrolusSous.RateLimiting;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace KyrolusSous.RateLimiting.UnitTests;

public sealed class ArchitecturePartitionKeysTests
{
    private static readonly IPAddress BaseIp = IPAddress.Parse("198.51.100.42");

    [Fact(DisplayName = "Composite Key generates joined partition keys accurately")]
    public void CompositeKey_GeneratesDelimitedSegmentsAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;
        context.Request.Host = new HostString("api.acme.com");
        context.Request.Protocol = "HTTP/3";
        context.Request.Headers["CF-IPCountry"] = "EG";

        var composite = KyrolusRateLimitPartitionKeys.GetCompositeKey(
            context,
            delimiter: "|",
            ctx => KyrolusRateLimitPartitionKeys.GetHostKey(ctx),
            ctx => KyrolusRateLimitPartitionKeys.GetGeoCountryKey(ctx),
            ctx => KyrolusRateLimitPartitionKeys.GetProtocolAndClientKey(ctx));

        composite.ShouldBe("host_api.acme.com|country_EG|proto_HTTP/3:198.51.100.42");
    }

    [Fact(DisplayName = "Request Cost, GraphQL Operation, and User-Agent Engine resolve accurately")]
    public void Cost_GraphQl_And_UserAgentEngine_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Request Cost
        context.Request.Headers["X-Query-Complexity"] = "150";
        KyrolusRateLimitPartitionKeys.GetRequestCostKey(context).ShouldBe("cost_heavy");
        KyrolusRateLimitPartitionKeys.GetRequestCostAndClientKey(context).ShouldBe("cost_heavy:198.51.100.42");

        context.Request.Headers["X-Query-Complexity"] = "50";
        KyrolusRateLimitPartitionKeys.GetRequestCostKey(context).ShouldBe("cost_medium");

        // GraphQL Operation
        var gqlContext = new DefaultHttpContext();
        gqlContext.Connection.RemoteIpAddress = BaseIp;
        gqlContext.Request.Headers["X-GraphQL-Operation-Name"] = "GetUserBillingReport";
        KyrolusRateLimitPartitionKeys.GetGraphQlOperationKey(gqlContext).ShouldBe("gql_GetUserBillingReport");
        KyrolusRateLimitPartitionKeys.GetGraphQlOperationAndClientKey(gqlContext).ShouldBe("gql_GetUserBillingReport:198.51.100.42");

        // User-Agent Engine
        var uaContext = new DefaultHttpContext();
        uaContext.Connection.RemoteIpAddress = BaseIp;
        uaContext.Request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        KyrolusRateLimitPartitionKeys.GetUserAgentEngineKey(uaContext).ShouldBe("engine_browser");
        KyrolusRateLimitPartitionKeys.GetUserAgentEngineAndClientKey(uaContext).ShouldBe("engine_browser:198.51.100.42");
    }

    [Fact(DisplayName = "TimeWindow, Priority, Correlation, W3C Baggage, and Load Shedding resolve accurately")]
    public void Time_Priority_Trace_Baggage_And_Shedding_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Time Window
        KyrolusRateLimitPartitionKeys.GetTimeWindowKey(context).ShouldNotBeNull();
        KyrolusRateLimitPartitionKeys.GetTimeWindowAndClientKey(context).ShouldNotBeNull();

        // Priority
        context.Request.Headers["X-Priority"] = "high";
        KyrolusRateLimitPartitionKeys.GetPriorityKey(context).ShouldBe("priority_critical");
        KyrolusRateLimitPartitionKeys.GetPriorityAndClientKey(context).ShouldBe("priority_critical:198.51.100.42");

        // Correlation
        var corrContext = new DefaultHttpContext();
        corrContext.Connection.RemoteIpAddress = BaseIp;
        corrContext.Request.Headers["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        KyrolusRateLimitPartitionKeys.GetCorrelationKey(corrContext).ShouldBe("trace_4bf92f3577b34da6a3ce929d0e0e4736");
        KyrolusRateLimitPartitionKeys.GetCorrelationAndClientKey(corrContext).ShouldBe("trace_4bf92f3577b34da6a3ce929d0e0e4736:198.51.100.42");

        // W3C Baggage
        var bagContext = new DefaultHttpContext();
        bagContext.Connection.RemoteIpAddress = BaseIp;
        bagContext.Request.Headers["baggage"] = "tier=gold,experiment=canary-v2";
        KyrolusRateLimitPartitionKeys.GetW3cBaggageKey(bagContext, "tier").ShouldBe("baggage_tier_gold");
        KyrolusRateLimitPartitionKeys.GetW3cBaggageAndClientKey(bagContext, "tier").ShouldBe("baggage_tier_gold:198.51.100.42");

        // Load Shedding
        var shedContext = new DefaultHttpContext();
        shedContext.Connection.RemoteIpAddress = BaseIp;
        shedContext.Request.Headers["X-System-Load"] = "shedding";
        KyrolusRateLimitPartitionKeys.GetLoadSheddingStateKey(shedContext).ShouldBe("state_degraded");
        KyrolusRateLimitPartitionKeys.GetLoadSheddingStateAndClientKey(shedContext).ShouldBe("state_degraded:198.51.100.42");
    }

    [Fact(DisplayName = "Webhook Target Domain, Attempt Count, Async Prefer, and Cloud Region resolve accurately")]
    public void Webhook_Async_And_Region_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Webhook Target Domain
        context.Request.Headers["X-Webhook-Target"] = "https://partner.client.com/webhook/orders";
        KyrolusRateLimitPartitionKeys.GetWebhookTargetDomainKey(context).ShouldBe("webhook_partner.client.com");
        KyrolusRateLimitPartitionKeys.GetWebhookTargetDomainAndClientKey(context).ShouldBe("webhook_partner.client.com:198.51.100.42");

        // Webhook Attempt Count
        var whContext = new DefaultHttpContext();
        whContext.Connection.RemoteIpAddress = BaseIp;
        whContext.Request.Headers["Stripe-Delivery-Attempt"] = "4";
        KyrolusRateLimitPartitionKeys.GetWebhookAttemptCountKey(whContext).ShouldBe("attempt_retry_low");
        KyrolusRateLimitPartitionKeys.GetWebhookAttemptCountAndClientKey(whContext).ShouldBe("attempt_retry_low:198.51.100.42");

        // RFC 7240 Async Prefer
        var asyncContext = new DefaultHttpContext();
        asyncContext.Connection.RemoteIpAddress = BaseIp;
        asyncContext.Request.Headers["Prefer"] = "respond-async, wait=10";
        KyrolusRateLimitPartitionKeys.GetAsyncExecutionModeKey(asyncContext).ShouldBe("exec_async");
        KyrolusRateLimitPartitionKeys.GetAsyncExecutionModeAndClientKey(asyncContext).ShouldBe("exec_async:198.51.100.42");

        // Cloud Origin Region
        var regionContext = new DefaultHttpContext();
        regionContext.Connection.RemoteIpAddress = BaseIp;
        regionContext.Request.Headers["X-Region"] = "us-east-1";
        KyrolusRateLimitPartitionKeys.GetOriginRegionKey(regionContext).ShouldBe("region_us-east-1");
        KyrolusRateLimitPartitionKeys.GetOriginRegionAndClientKey(regionContext).ShouldBe("region_us-east-1:198.51.100.42");
    }

    [Fact(DisplayName = "W3C Fetch Metadata, Conditional Requests, and Cache Policy resolve accurately")]
    public void FetchMetadata_Conditional_And_CachePolicy_ResolveAccurately()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = BaseIp;

        // Fetch Metadata Site
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        KyrolusRateLimitPartitionKeys.GetSecFetchSiteKey(context).ShouldBe("site_same-origin");
        KyrolusRateLimitPartitionKeys.GetSecFetchSiteAndClientKey(context).ShouldBe("site_same-origin:198.51.100.42");

        // Fetch Metadata Mode
        context.Request.Headers["Sec-Fetch-Mode"] = "cors";
        KyrolusRateLimitPartitionKeys.GetSecFetchModeKey(context).ShouldBe("fetch_mode_cors");
        KyrolusRateLimitPartitionKeys.GetSecFetchModeAndClientKey(context).ShouldBe("fetch_mode_cors:198.51.100.42");

        // Fetch Metadata Dest
        context.Request.Headers["Sec-Fetch-Dest"] = "document";
        KyrolusRateLimitPartitionKeys.GetSecFetchDestKey(context).ShouldBe("dest_document");
        KyrolusRateLimitPartitionKeys.GetSecFetchDestAndClientKey(context).ShouldBe("dest_document:198.51.100.42");

        // Conditional Requests
        var condContext = new DefaultHttpContext();
        condContext.Connection.RemoteIpAddress = BaseIp;
        condContext.Request.Headers["If-None-Match"] = "\"etag_abc123\"";
        KyrolusRateLimitPartitionKeys.GetConditionalRequestTypeKey(condContext).ShouldBe("cond_etag");
        KyrolusRateLimitPartitionKeys.GetConditionalRequestTypeAndClientKey(condContext).ShouldBe("cond_etag:198.51.100.42");

        // Cache Policy
        var cacheContext = new DefaultHttpContext();
        cacheContext.Connection.RemoteIpAddress = BaseIp;
        cacheContext.Request.Headers["Cache-Control"] = "no-cache, no-store";
        KyrolusRateLimitPartitionKeys.GetCachePolicyKey(cacheContext).ShouldBe("cache_bypass");
        KyrolusRateLimitPartitionKeys.GetCachePolicyAndClientKey(cacheContext).ShouldBe("cache_bypass:198.51.100.42");
    }
}
