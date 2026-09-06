using KyrolusSous.EndpointKit.Core.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusEndpointSecurityFilterTests
{
    private sealed class DummyEndpointFilterInvocationContext : EndpointFilterInvocationContext
    {
        public DummyEndpointFilterInvocationContext(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public override HttpContext HttpContext { get; }
        public override IList<object?> Arguments => [];
        public override T GetArgument<T>(int index) => throw new NotImplementedException();
    }

    [Fact(DisplayName = "PayloadSizeEndpointFilter: Rejects payload exceeding max size with 413")]
    public async Task PayloadSizeFilter_RejectsOversizedPayload()
    {
        var filter = new KyrolusPayloadSizeEndpointFilter(1024); // 1 KB
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentLength = 2048; // 2 KB

        var context = new DummyEndpointFilterInvocationContext(httpContext);
        var invoked = false;
        EndpointFilterDelegate next = _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>("OK");
        };

        var result = await filter.InvokeAsync(context, next);

        invoked.ShouldBeFalse();
        result.ShouldBeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact(DisplayName = "PayloadSizeEndpointFilter: Allows payload within max size")]
    public async Task PayloadSizeFilter_AllowsValidPayload()
    {
        var filter = new KyrolusPayloadSizeEndpointFilter(1024);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentLength = 512;

        var context = new DummyEndpointFilterInvocationContext(httpContext);
        var invoked = false;
        EndpointFilterDelegate next = _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>("OK");
        };

        var result = await filter.InvokeAsync(context, next);

        invoked.ShouldBeTrue();
        result.ShouldBe("OK");
    }

    [Fact(DisplayName = "HeaderLimitsEndpointFilter: Rejects when header count exceeded with 431")]
    public async Task HeaderLimitsFilter_RejectsExcessiveHeaderCount()
    {
        var filter = new KyrolusHeaderLimitsEndpointFilter(maxHeaderCount: 3, maxTotalHeaderSizeBytes: 1024);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["H1"] = "v1";
        httpContext.Request.Headers["H2"] = "v2";
        httpContext.Request.Headers["H3"] = "v3";
        httpContext.Request.Headers["H4"] = "v4";

        var context = new DummyEndpointFilterInvocationContext(httpContext);
        var invoked = false;
        EndpointFilterDelegate next = _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>("OK");
        };

        var result = await filter.InvokeAsync(context, next);

        invoked.ShouldBeFalse();
        result.ShouldBeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.ShouldBe(StatusCodes.Status431RequestHeaderFieldsTooLarge);
    }

    [Fact(DisplayName = "HeaderLimitsEndpointFilter: Rejects when total header size exceeded with 431")]
    public async Task HeaderLimitsFilter_RejectsExcessiveHeaderSize()
    {
        var filter = new KyrolusHeaderLimitsEndpointFilter(maxHeaderCount: 10, maxTotalHeaderSizeBytes: 50);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Large"] = new string('A', 100);

        var context = new DummyEndpointFilterInvocationContext(httpContext);
        var invoked = false;
        EndpointFilterDelegate next = _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>("OK");
        };

        var result = await filter.InvokeAsync(context, next);

        invoked.ShouldBeFalse();
        result.ShouldBeOfType<ProblemHttpResult>();
        var problem = (ProblemHttpResult)result;
        problem.StatusCode.ShouldBe(StatusCodes.Status431RequestHeaderFieldsTooLarge);
    }

    [Fact(DisplayName = "HeaderLimitsEndpointFilter: Allows request within limits")]
    public async Task HeaderLimitsFilter_AllowsValidRequest()
    {
        var filter = new KyrolusHeaderLimitsEndpointFilter(maxHeaderCount: 10, maxTotalHeaderSizeBytes: 1024);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test"] = "NormalValue";

        var context = new DummyEndpointFilterInvocationContext(httpContext);
        var invoked = false;
        EndpointFilterDelegate next = _ =>
        {
            invoked = true;
            return ValueTask.FromResult<object?>("OK");
        };

        var result = await filter.InvokeAsync(context, next);

        invoked.ShouldBeTrue();
        result.ShouldBe("OK");
    }
}
