using System.Security.Claims;
using System.Text;
using System.Text.Json;
using KyrolusSous.EndpointKit.Core.Conditional;
using KyrolusSous.EndpointKit.Core.Filters;
using KyrolusSous.EndpointKit.Core.Pagination;
using KyrolusSous.EndpointKit.Core.Patch;
using KyrolusSous.EndpointKit.Core.Streaming;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusEndpointKitExtendedFeaturesTests
{
    [Fact(DisplayName = "TenantFilter: Extracts TenantId from Header or Claims and populates HttpContext.Items")]
    public async Task TenantFilter_Should_Resolve_Tenant_From_Header()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = "tenant-abc-123";

        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);

        var filter = new KyrolusTenantEndpointFilter();
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("Success");
        };

        var result = await filter.InvokeAsync(filterContext, next);
        nextCalled.ShouldBeTrue();
        httpContext.Items[KyrolusTenantEndpointFilter.TenantItemKey].ShouldBe("tenant-abc-123");
    }

    [Fact(DisplayName = "TenantFilter: Rejects request with 400 when requireTenant is true and no tenant is supplied")]
    public async Task TenantFilter_Should_ShortCircuit_When_Required_Tenant_Missing()
    {
        var httpContext = new DefaultHttpContext();
        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);

        var filter = new KyrolusTenantEndpointFilter(requireTenant: true);
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("Success");
        };

        var result = await filter.InvokeAsync(filterContext, next);
        nextCalled.ShouldBeFalse();
        result.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Cursor: Encodes and decodes keyset pagination cursor properly")]
    public void Cursor_Should_Encode_And_Decode()
    {
        var key = 1050;
        var secondary = "2026-08-25T10:00:00Z";

        var cursor = KyrolusCursor.Encode(key, secondary);
        cursor.ShouldNotBeNullOrWhiteSpace();

        var success = KyrolusCursor.TryDecode<int>(cursor, out var decodedKey, out var decodedSecondary);
        success.ShouldBeTrue();
        decodedKey.ShouldBe(1050);
        decodedSecondary.ShouldBe("2026-08-25T10:00:00Z");
    }

    [Fact(DisplayName = "ConditionalRequest: Evaluates If-None-Match for 304 and If-Match for 412")]
    public void ConditionalRequest_Should_Evaluate_Headers()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-None-Match"] = "\"etag-123\"";
        KyrolusConditionalRequest.IsNotModified(httpContext.Request, "\"etag-123\"").ShouldBeTrue();
        KyrolusConditionalRequest.IsNotModified(httpContext.Request, "\"etag-other\"").ShouldBeFalse();

        httpContext.Request.Headers.Clear();
        httpContext.Request.Headers["If-Match"] = "\"etag-123\"";
        KyrolusConditionalRequest.IsPreconditionFailed(httpContext.Request, "\"etag-123\"").ShouldBeFalse();
        KyrolusConditionalRequest.IsPreconditionFailed(httpContext.Request, "\"etag-modified\"").ShouldBeTrue();
    }

    [Fact(DisplayName = "JsonMergePatch: Parses RFC 7396 document distinguishing null from omitted properties")]
    public void JsonMergePatch_Should_Parse_Properties()
    {
        var json = """
        {
            "name": "Updated Name",
            "description": null,
            "count": 42
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var patch = KyrolusJsonMergePatch.ParseMergePatch(doc.RootElement);

        patch.ContainsKey("name").ShouldBeTrue();
        patch["name"].ShouldBe("Updated Name");

        patch.ContainsKey("description").ShouldBeTrue();
        patch["description"].ShouldBeNull(); // Explicitly nullified

        patch.ContainsKey("count").ShouldBeTrue();
        patch["count"].ShouldBe(42L);

        patch.ContainsKey("otherProperty").ShouldBeFalse(); // Omitted
    }

    [Fact(DisplayName = "SSE: Streams items formatted as text/event-stream")]
    public async Task SseResult_Should_Format_Stream()
    {
        async IAsyncEnumerable<string> GetItemsAsync()
        {
            yield return "Event 1";
            yield return "Event 2";
        }

        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var sse = new KyrolusSseResult<string>(GetItemsAsync(), eventType: "message");
        await sse.ExecuteAsync(httpContext);

        httpContext.Response.ContentType!.ShouldContain("text/event-stream");
        responseBody.Seek(0, SeekOrigin.Begin);
        var text = new StreamReader(responseBody).ReadToEnd();

        text.ShouldContain("event: message\ndata: \"Event 1\"\n\n");
        text.ShouldContain("event: message\ndata: \"Event 2\"\n\n");
    }
}
