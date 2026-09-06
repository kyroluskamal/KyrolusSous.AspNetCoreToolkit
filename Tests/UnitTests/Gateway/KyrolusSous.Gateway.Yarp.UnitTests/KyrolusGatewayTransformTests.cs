using KyrolusSous.Gateway.Abstractions;
using KyrolusSous.Gateway.Yarp.Configuration;
using Shouldly;
using Xunit;

namespace KyrolusSous.Gateway.Yarp.UnitTests;

public class KyrolusGatewayTransformTests
{
    [Fact(DisplayName = "Path Transforms Factory Methods Set Expected Dictionary Keys")]
    public void PathTransforms_FactoryMethods_SetExpectedDictionaryKeys()
    {
        var removePrefix = KyrolusGatewayTransform.PathRemovePrefix("/api");
        removePrefix[KyrolusGatewayTransformNames.PathRemovePrefix].ShouldBe("/api");
        removePrefix.Count.ShouldBe(1);

        var prefix = KyrolusGatewayTransform.PathPrefix("/v1");
        prefix[KyrolusGatewayTransformNames.PathPrefix].ShouldBe("/v1");

        var set = KyrolusGatewayTransform.PathSet("/healthz");
        set[KyrolusGatewayTransformNames.PathSet].ShouldBe("/healthz");

        var pattern = KyrolusGatewayTransform.PathPattern("/api/{**remainder}");
        pattern[KyrolusGatewayTransformNames.PathPattern].ShouldBe("/api/{**remainder}");
    }

    [Fact(DisplayName = "Path Transforms Validate Arguments")]
    public void PathTransforms_ValidateArguments()
    {
        Should.Throw<ArgumentException>(() => KyrolusGatewayTransform.PathRemovePrefix(""));
        Should.Throw<ArgumentException>(() => KyrolusGatewayTransform.PathPrefix("   "));
        Should.Throw<ArgumentException>(() => KyrolusGatewayTransform.PathSet(null!));
        Should.Throw<ArgumentException>(() => KyrolusGatewayTransform.PathPattern(""));
    }

    [Fact(DisplayName = "Request Header Transforms Set Correct Keys And Enums")]
    public void RequestHeaderTransforms_SetCorrectKeysAndEnums()
    {
        var reqHeaderSet = KyrolusGatewayTransform.RequestHeader("X-Trace", "123", KyrolusTransformAction.Set);
        reqHeaderSet[KyrolusGatewayTransformNames.RequestHeader].ShouldBe("X-Trace");
        reqHeaderSet["Set"].ShouldBe("123");

        var reqHeaderAppend = KyrolusGatewayTransform.RequestHeader("X-Tags", "tag1", KyrolusTransformAction.Append);
        reqHeaderAppend[KyrolusGatewayTransformNames.RequestHeader].ShouldBe("X-Tags");
        reqHeaderAppend["Append"].ShouldBe("tag1");

        var removeHeader = KyrolusGatewayTransform.RequestHeaderRemove("Authorization");
        removeHeader[KyrolusGatewayTransformNames.RequestHeaderRemove].ShouldBe("Authorization");

        var allowedHeaders = KyrolusGatewayTransform.RequestHeadersAllowed("Host", "Authorization", "X-Trace");
        allowedHeaders[KyrolusGatewayTransformNames.RequestHeadersAllowed].ShouldBe("Host;Authorization;X-Trace");

        var origHostTrue = KyrolusGatewayTransform.RequestHeaderOriginalHost(true);
        origHostTrue[KyrolusGatewayTransformNames.RequestHeaderOriginalHost].ShouldBe("true");

        var origHostFalse = KyrolusGatewayTransform.RequestHeaderOriginalHost(false);
        origHostFalse[KyrolusGatewayTransformNames.RequestHeaderOriginalHost].ShouldBe("false");

        var clientCert = KyrolusGatewayTransform.ClientCert("X-Client-Cert");
        clientCert[KyrolusGatewayTransformNames.ClientCert].ShouldBe("X-Client-Cert");
    }

    [Fact(DisplayName = "Response Header Transforms Set Correct Keys And Conditions")]
    public void ResponseHeaderTransforms_SetCorrectKeysAndConditions()
    {
        var respHeader = KyrolusGatewayTransform.ResponseHeader("X-Service", "OrderService", KyrolusTransformAction.Set, KyrolusTransformWhen.Success);
        respHeader[KyrolusGatewayTransformNames.ResponseHeader].ShouldBe("X-Service");
        respHeader["Set"].ShouldBe("OrderService");
        respHeader["When"].ShouldBe("Success");

        var respHeaderValue = KyrolusGatewayTransform.ResponseHeaderValue("X-Gateway", "Active");
        respHeaderValue[KyrolusGatewayTransformNames.ResponseHeaderValue].ShouldBe("X-Gateway");
        respHeaderValue["Set"].ShouldBe("Active");
        respHeaderValue["When"].ShouldBe("Always");

        var removeHeader = KyrolusGatewayTransform.ResponseHeaderRemove("Server");
        removeHeader[KyrolusGatewayTransformNames.ResponseHeaderRemove].ShouldBe("Server");
        removeHeader["When"].ShouldBe("Always");

        var allowedHeaders = KyrolusGatewayTransform.ResponseHeadersAllowed("Content-Type", "Content-Length");
        allowedHeaders[KyrolusGatewayTransformNames.ResponseHeadersAllowed].ShouldBe("Content-Type;Content-Length");

        var allowedTrailers = KyrolusGatewayTransform.ResponseTrailersAllowed("ETag");
        allowedTrailers[KyrolusGatewayTransformNames.ResponseTrailersAllowed].ShouldBe("ETag");
    }

    [Fact(DisplayName = "Query Parameter Transforms Set Expected Keys")]
    public void QueryParameterTransforms_SetExpectedKeys()
    {
        var valParam = KyrolusGatewayTransform.QueryValueParameter("api_key", "secret-123", KyrolusTransformAction.Set);
        valParam[KyrolusGatewayTransformNames.QueryValueParameter].ShouldBe("api_key");
        valParam["Set"].ShouldBe("secret-123");

        var routeParam = KyrolusGatewayTransform.QueryRouteParameter("q", "searchQuery", KyrolusTransformAction.Append);
        routeParam[KyrolusGatewayTransformNames.QueryRouteParameter].ShouldBe("q");
        routeParam["Append"].ShouldBe("searchQuery");

        var removeParam = KyrolusGatewayTransform.QueryRemoveParameter("debug");
        removeParam[KyrolusGatewayTransformNames.QueryRemoveParameter].ShouldBe("debug");
    }

    [Fact(DisplayName = "Forwarded And Custom Transforms Work Correctly")]
    public void ForwardedAndCustomTransforms_WorkCorrectly()
    {
        var fwd = KyrolusGatewayTransform.Forwarded("proto,host,for", "Random", "Forwarded");
        fwd[KyrolusGatewayTransformNames.Forwarded].ShouldBe("proto,host,for");
        fwd["ForFormat"].ShouldBe("Random");

        var xfwd = KyrolusGatewayTransform.XForwarded("Set", "Random");
        xfwd[KyrolusGatewayTransformNames.XForwarded].ShouldBe("Set");
        xfwd["ForFormat"].ShouldBe("Random");

        var custom = KyrolusGatewayTransform.Custom("TenantHeader", "X-Tenant-Id");
        custom["TenantHeader"].ShouldBe("X-Tenant-Id");
        custom.Count.ShouldBe(1);

        var customDict = KyrolusGatewayTransform.Custom(new Dictionary<string, string>
        {
            ["CustomKey"] = "CustomValue",
            ["Extra"] = "1"
        });
        customDict.Count.ShouldBe(2);
        customDict["CustomKey"].ShouldBe("CustomValue");
    }

    [Fact(DisplayName = "Default Struct Value Does Not Throw NullReferenceException")]
    public void DefaultStructValue_DoesNotThrowNullReferenceException()
    {
        var defaultTransform = default(KyrolusGatewayTransform);
        defaultTransform.Count.ShouldBe(0);
        defaultTransform.ContainsKey("any").ShouldBeFalse();
        defaultTransform.TryGetValue("any", out var val).ShouldBeFalse();
        val.ShouldBeNull();
        defaultTransform.GetEnumerator().MoveNext().ShouldBeFalse();
    }

    [Fact(DisplayName = "Implicit Operators Convert To And From Dictionary Seamlessly")]
    public void ImplicitOperators_ConvertSeamlessly()
    {
        var transform = KyrolusGatewayTransform.PathRemovePrefix("/api");
        Dictionary<string, string> dict = transform;
        dict["PathRemovePrefix"].ShouldBe("/api");

        KyrolusGatewayTransform fromDict = dict;
        fromDict["PathRemovePrefix"].ShouldBe("/api");
        fromDict.Equals(transform).ShouldBeTrue();
        (fromDict == transform).ShouldBeTrue();
    }

    [Fact(DisplayName = "Equality And HashCode Function Reliably")]
    public void EqualityAndHashCode_FunctionReliably()
    {
        var t1 = KyrolusGatewayTransform.PathRemovePrefix("/api");
        var t2 = KyrolusGatewayTransform.PathRemovePrefix("/api");
        var t3 = KyrolusGatewayTransform.PathRemovePrefix("/v2");

        t1.ShouldBe(t2);
        t1.GetHashCode().ShouldBe(t2.GetHashCode());
        (t1 == t2).ShouldBeTrue();
        (t1 != t3).ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusRouteBuilder Supports Strongly Typed WithTransform And WithTransforms")]
    public void RouteBuilder_SupportsStronglyTypedTransforms()
    {
        var builder = new KyrolusRouteBuilder("orders-route", "orders-cluster", "/api/orders")
            .WithTransform(KyrolusGatewayTransform.PathRemovePrefix("/api"))
            .WithTransforms(
                KyrolusGatewayTransform.RequestHeader("X-Trace", "123"),
                KyrolusGatewayTransform.ResponseHeader("X-Gateway", "Active")
            );

        var route = builder.Build();
        route.Transforms.ShouldNotBeNull();
        route.Transforms.Count.ShouldBe(3);
        route.Transforms[0][KyrolusGatewayTransformNames.PathRemovePrefix].ShouldBe("/api");
        route.Transforms[1][KyrolusGatewayTransformNames.RequestHeader].ShouldBe("X-Trace");
        route.Transforms[2][KyrolusGatewayTransformNames.ResponseHeader].ShouldBe("X-Gateway");
        route.Transforms[2]["Set"].ShouldBe("Active");
    }

    [Fact(DisplayName = "Header And Query Match Mode Structs Support Standard Values And Conversions")]
    public void MatchModeStructs_SupportStandardValuesAndConversions()
    {
        KyrolusHeaderMatchMode headerExact = "ExactHeader";
        headerExact.ShouldBe(KyrolusHeaderMatchMode.ExactHeader);
        headerExact.Value.ShouldBe("ExactHeader");

        KyrolusHeaderMatchMode customHeader = KyrolusHeaderMatchMode.Custom("Regex");
        customHeader.Value.ShouldBe("Regex");
        string customHeaderStr = customHeader;
        customHeaderStr.ShouldBe("Regex");

        KyrolusQueryParamMatchMode queryExact = "Exact";
        queryExact.ShouldBe(KyrolusQueryParamMatchMode.Exact);

        KyrolusQueryParamMatchMode queryPrefix = KyrolusQueryParamMatchMode.Prefix;
        queryPrefix.Value.ShouldBe("Prefix");
        string queryPrefixStr = queryPrefix;
        queryPrefixStr.ShouldBe("Prefix");
    }

    [Fact(DisplayName = "Session Affinity Policy Structs Support Standard Values And Implicit Conversions")]
    public void SessionAffinityPolicyStructs_SupportStandardValuesAndConversions()
    {
        KyrolusSessionAffinityPolicy cookiePolicy = "Cookie";
        cookiePolicy.ShouldBe(KyrolusSessionAffinityPolicy.Cookie);
        cookiePolicy.Value.ShouldBe("Cookie");

        KyrolusSessionAffinityPolicy customAffinity = KyrolusSessionAffinityPolicy.Custom("HeaderToken");
        customAffinity.Value.ShouldBe("HeaderToken");

        KyrolusSessionAffinityFailurePolicy failurePolicy = "Redistribute";
        failurePolicy.ShouldBe(KyrolusSessionAffinityFailurePolicy.Redistribute);

        KyrolusSessionAffinityFailurePolicy error503 = KyrolusSessionAffinityFailurePolicy.Return503Error;
        error503.Value.ShouldBe("Return503Error");
    }

    [Fact(DisplayName = "Authorization Policy Struct Supports Standard Values And Custom Values")]
    public void AuthorizationPolicyStruct_SupportsStandardAndCustomValues()
    {
        KyrolusAuthorizationPolicy anon = KyrolusAuthorizationPolicy.Anonymous;
        anon.Value.ShouldBe("anonymous");
        (anon == "anonymous").ShouldBeTrue();

        KyrolusAuthorizationPolicy def = KyrolusAuthorizationPolicy.Default;
        def.Value.ShouldBe("default");

        KyrolusAuthorizationPolicy custom = KyrolusAuthorizationPolicy.Custom("AdminOnly");
        custom.Value.ShouldBe("AdminOnly");
        string customStr = custom;
        customStr.ShouldBe("AdminOnly");

        KyrolusAuthorizationPolicy.From(null).ShouldBeNull();
        KyrolusAuthorizationPolicy.From("anonymous").ShouldBe(KyrolusAuthorizationPolicy.Anonymous);
    }

    [Fact(DisplayName = "Cors And RateLimiter Policy Structs Support Standard Values And Builder Convenience Methods")]
    public void CorsAndRateLimiterPolicyStructs_SupportStandardValuesAndBuilderMethods()
    {
        KyrolusCorsPolicy corsDisable = KyrolusCorsPolicy.Disable;
        corsDisable.Value.ShouldBe("disable");
        (corsDisable == "disable").ShouldBeTrue();

        KyrolusRateLimiterPolicy rateDisable = KyrolusRateLimiterPolicy.Disable;
        rateDisable.Value.ShouldBe("disable");

        KyrolusOutputCachePolicy cachePolicy = KyrolusOutputCachePolicy.Custom("Expire5M");
        cachePolicy.Value.ShouldBe("Expire5M");

        var route = new KyrolusRouteBuilder("test-route", "test-cluster", "/api/test")
            .WithAnonymousAuthorization()
            .WithDisabledCors()
            .WithDisabledRateLimiter()
            .WithOutputCache(cachePolicy)
            .Build();

        route.AuthorizationPolicy.ShouldBe(KyrolusAuthorizationPolicy.Anonymous);
        route.CorsPolicy.ShouldBe(KyrolusCorsPolicy.Disable);
        route.RateLimiterPolicy.ShouldBe(KyrolusRateLimiterPolicy.Disable);
        route.OutputCachePolicy.ShouldBe(cachePolicy);
    }
}
