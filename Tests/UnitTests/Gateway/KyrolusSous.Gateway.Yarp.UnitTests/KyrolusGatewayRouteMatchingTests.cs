using KyrolusSous.Gateway.Abstractions;
using KyrolusSous.Gateway.Yarp.Configuration;
using Shouldly;
using Xunit;

namespace KyrolusSous.Gateway.Yarp.UnitTests;

public class KyrolusGatewayRouteMatchingTests
{
    #region KyrolusHttpMethod Tests

    [Fact(DisplayName = "KyrolusHttpMethod: All standard RFC verbs expose correct values")]
    public void KyrolusHttpMethod_AllStandardVerbs_ExposeCorrectValues()
    {
        KyrolusHttpMethod.Get.Value.ShouldBe("GET");
        KyrolusHttpMethod.Post.Value.ShouldBe("POST");
        KyrolusHttpMethod.Put.Value.ShouldBe("PUT");
        KyrolusHttpMethod.Delete.Value.ShouldBe("DELETE");
        KyrolusHttpMethod.Patch.Value.ShouldBe("PATCH");
        KyrolusHttpMethod.Head.Value.ShouldBe("HEAD");
        KyrolusHttpMethod.Options.Value.ShouldBe("OPTIONS");
        KyrolusHttpMethod.Trace.Value.ShouldBe("TRACE");
        KyrolusHttpMethod.Connect.Value.ShouldBe("CONNECT");

        KyrolusHttpMethod.AllStandardMethods.Count.ShouldBe(9);
    }

    [Theory(DisplayName = "KyrolusHttpMethod: From and Custom normalize casing and trimming")]
    [InlineData("get", "GET")]
    [InlineData("Post", "POST")]
    [InlineData("  put  ", "PUT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("patch", "PATCH")]
    [InlineData("purge", "PURGE")]
    public void KyrolusHttpMethod_FromAndCustom_NormalizeCasingAndTrimming(string input, string expected)
    {
        var method = KyrolusHttpMethod.From(input);
        method.ShouldNotBeNull();
        method.Value.Value.ShouldBe(expected);

        var custom = KyrolusHttpMethod.Custom(input);
        custom.Value.ShouldBe(expected);
    }

    [Fact(DisplayName = "KyrolusHttpMethod: TryParse returns true for valid verbs and false for null/whitespace")]
    public void KyrolusHttpMethod_TryParse_WorksAsExpected()
    {
        KyrolusHttpMethod.TryParse("get", out var method).ShouldBeTrue();
        method.Value.ShouldBe("GET");

        KyrolusHttpMethod.TryParse(null, out _).ShouldBeFalse();
        KyrolusHttpMethod.TryParse("   ", out _).ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusHttpMethod: Implicit conversions to and from string work seamlessly")]
    public void KyrolusHttpMethod_ImplicitConversions_WorkSeamlessly()
    {
        string verb = KyrolusHttpMethod.Get;
        verb.ShouldBe("GET");

        KyrolusHttpMethod method = "post";
        method.Value.ShouldBe("POST");
        method.ShouldBe(KyrolusHttpMethod.Post);
    }

    [Fact(DisplayName = "KyrolusHttpMethod: Equality and comparison work correctly")]
    public void KyrolusHttpMethod_EqualityAndComparison_WorkCorrectly()
    {
        (KyrolusHttpMethod.Get == KyrolusHttpMethod.Get).ShouldBeTrue();
        (KyrolusHttpMethod.Get != KyrolusHttpMethod.Post).ShouldBeTrue();
        KyrolusHttpMethod.Get.Equals("get").ShouldBeTrue();
        KyrolusHttpMethod.Get.Equals("GET").ShouldBeTrue();
        KyrolusHttpMethod.Get.Equals("POST").ShouldBeFalse();

        KyrolusHttpMethod.Get.ToString().ShouldBe("GET");
    }

    #endregion

    #region KyrolusHostValidator Tests

    [Theory(DisplayName = "KyrolusHostValidator: Valid hosts are accepted and normalized to lowercase")]
    [InlineData("api.example.com", "api.example.com")]
    [InlineData("API.EXAMPLE.COM", "api.example.com")]
    [InlineData("  api.example.com  ", "api.example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("localhost", "localhost")]
    [InlineData("localhost:5000", "localhost:5000")]
    [InlineData("api.example.com:8080", "api.example.com:8080")]
    [InlineData("*.example.com", "*.example.com")]
    [InlineData("*.api.example.com", "*.api.example.com")]
    [InlineData("*", "*")]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("127.0.0.1:8080", "127.0.0.1:8080")]
    [InlineData("192.168.1.100:443", "192.168.1.100:443")]
    [InlineData("[::1]", "[::1]")]
    [InlineData("[::1]:5000", "[::1]:5000")]
    [InlineData("[2001:db8::1]", "[2001:db8::1]")]
    [InlineData("orders-service", "orders-service")]
    [InlineData("orders-service:80", "orders-service:80")]
    public void KyrolusHostValidator_ValidHosts_AreAcceptedAndNormalized(string input, string expected)
    {
        KyrolusHostValidator.Validate(input).ShouldBe(expected);
        KyrolusHostValidator.IsValid(input).ShouldBeTrue();

        var success = KyrolusHostValidator.TryValidate(input, out var normalized, out var error);
        success.ShouldBeTrue();
        normalized.ShouldBe(expected);
        error.ShouldBeNull();
    }

    [Theory(DisplayName = "KyrolusHostValidator: Rejects URI schemes with specific message")]
    [InlineData("http://api.example.com")]
    [InlineData("https://api.example.com")]
    [InlineData("ws://localhost:5000")]
    [InlineData("wss://api.example.com")]
    [InlineData("http://127.0.0.1:5000")]
    public void KyrolusHostValidator_RejectsUriSchemes(string hostWithScheme)
    {
        var ex = Should.Throw<ArgumentException>(() => KyrolusHostValidator.Validate(hostWithScheme));
        ex.Message.ShouldContain("scheme");
        KyrolusHostValidator.IsValid(hostWithScheme).ShouldBeFalse();
    }

    [Theory(DisplayName = "KyrolusHostValidator: Rejects path slashes with specific message")]
    [InlineData("api.example.com/")]
    [InlineData("api.example.com/api/orders")]
    [InlineData("localhost:5000/")]
    [InlineData("api.example.com\\orders")]
    public void KyrolusHostValidator_RejectsPathSlashes(string hostWithPath)
    {
        var ex = Should.Throw<ArgumentException>(() => KyrolusHostValidator.Validate(hostWithPath));
        ex.Message.ShouldContain("path");
        KyrolusHostValidator.IsValid(hostWithPath).ShouldBeFalse();
    }

    [Theory(DisplayName = "KyrolusHostValidator: Rejects query strings and fragments")]
    [InlineData("api.example.com?query=1")]
    [InlineData("api.example.com#section")]
    public void KyrolusHostValidator_RejectsQueryStringsAndFragments(string invalidHost)
    {
        Should.Throw<ArgumentException>(() => KyrolusHostValidator.Validate(invalidHost));
        KyrolusHostValidator.IsValid(invalidHost).ShouldBeFalse();
    }

    [Theory(DisplayName = "KyrolusHostValidator: Rejects malformed domains, invalid ports, and bad IPs")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("api .example.com")]
    [InlineData("api..example.com")]
    [InlineData("api.example.com:0")]
    [InlineData("api.example.com:65536")]
    [InlineData("api.example.com:abc")]
    [InlineData("999.999.999.999")]
    [InlineData("[invalid-ipv6]")]
    [InlineData("-api.example.com")]
    [InlineData("api-.example.com")]
    public void KyrolusHostValidator_RejectsMalformedHosts(string malformedHost)
    {
        Should.Throw<ArgumentException>(() => KyrolusHostValidator.Validate(malformedHost));
        KyrolusHostValidator.IsValid(malformedHost).ShouldBeFalse();
    }

    #endregion

    #region KyrolusRouteHost Tests

    [Fact(DisplayName = "KyrolusRouteHost: Validates at construction and converts implicitly")]
    public void KyrolusRouteHost_ValidatesAtConstruction_AndConvertsImplicitly()
    {
        KyrolusRouteHost host = "Api.Example.Com";
        host.Value.ShouldBe("api.example.com");

        string raw = host;
        raw.ShouldBe("api.example.com");

        KyrolusRouteHost.Any.Value.ShouldBe("*");

        Should.Throw<ArgumentException>(() => new KyrolusRouteHost("https://bad.com"));

        KyrolusRouteHost.TryParse("api.example.com", out var parsed).ShouldBeTrue();
        parsed.Value.ShouldBe("api.example.com");

        KyrolusRouteHost.TryParse("https://bad.com", out _).ShouldBeFalse();
    }

    #endregion

    #region RouteBuilder Fluent Verbs and Host Validation Tests

    [Fact(DisplayName = "RouteBuilder: Strongly-typed WithMethods and convenience HTTP verbs configure route")]
    public void RouteBuilder_ConvenienceVerbs_ConfigureRoute()
    {
        var builder = new KyrolusRouteBuilder("test-route", "cluster1", "/api/test");
        builder.WithGet()
               .WithPost()
               .WithPut()
               .WithDelete()
               .WithPatch();

        var route = builder.Build();
        route.Match.Methods.ShouldNotBeNull();
        route.Match.Methods.Count.ShouldBe(5);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Get);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Post);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Put);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Delete);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Patch);
    }

    [Fact(DisplayName = "RouteBuilder: WithHosts accepts valid hosts and rejects invalid ones")]
    public void RouteBuilder_WithHosts_ValidatesCorrectly()
    {
        var builder = new KyrolusRouteBuilder("test-route", "cluster1", "/api/test");
        builder.WithHosts("api.example.com", "*.example.com", "localhost:5000");

        var route = builder.Build();
        route.Match.Hosts.ShouldNotBeNull();
        route.Match.Hosts.Count.ShouldBe(3);
        route.Match.Hosts.ShouldContain("api.example.com");
        route.Match.Hosts.ShouldContain("*.example.com");
        route.Match.Hosts.ShouldContain("localhost:5000");

        var invalidBuilder = new KyrolusRouteBuilder("invalid-route", "cluster1", "/api/test");
        Should.Throw<ArgumentException>(() => invalidBuilder.WithHosts("https://api.example.com"));
        Should.Throw<ArgumentException>(() => invalidBuilder.WithHost("api.example.com/path"));
    }

    [Fact(DisplayName = "ClusterBuilder: AddRoute with KyrolusHttpMethod and validated hosts works seamlessly")]
    public void ClusterBuilder_AddRoute_WithKyrolusHttpMethod_Works()
    {
        var builder = new KyrolusClusterBuilder("invoices-cluster");
        builder.AddRoute("get-invoices", "/api/invoices", KyrolusHttpMethod.Get, KyrolusHttpMethod.Post);

        var (_, routes) = builder.Build();
        routes.Count.ShouldBe(1);
        var route = routes[0];
        route.Match.Methods.ShouldNotBeNull();
        route.Match.Methods.Count.ShouldBe(2);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Get);
        route.Match.Methods.ShouldContain(KyrolusHttpMethod.Post);

        // Reject invalid scheme in AddRoute hosts
        var invalidBuilder = new KyrolusClusterBuilder("invalid-cluster");
        Should.Throw<ArgumentException>(() =>
            invalidBuilder.AddRoute("route1", "/api/test", methods: ["GET"], hosts: ["https://api.example.com"]));
    }

    #endregion
}
