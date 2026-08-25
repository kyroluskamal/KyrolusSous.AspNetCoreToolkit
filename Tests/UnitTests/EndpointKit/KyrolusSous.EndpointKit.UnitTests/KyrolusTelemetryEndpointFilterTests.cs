using System.Diagnostics;
using KyrolusSous.EndpointKit.Core.Filters;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusTelemetryEndpointFilterTests
{
    [Fact(DisplayName = "TelemetryFilter: Enriches active activity and executes next delegate")]
    public async Task TelemetryFilter_Should_Enrich_Activity()
    {
        using var activitySource = new ActivitySource("TestTelemetry");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("TestActivity");

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/products";

        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);

        var filter = new KyrolusTelemetryEndpointFilter("Product", "GetAll");

        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("Success");
        };

        var result = await filter.InvokeAsync(filterContext, next);
        nextCalled.ShouldBeTrue();
        result.ShouldBe("Success");

        if (activity is not null)
        {
            activity.GetTagItem("endpointkit.entity").ShouldBe("Product");
            activity.GetTagItem("endpointkit.action").ShouldBe("GetAll");
            activity.GetTagItem("endpointkit.route").ShouldBe("/api/products");
        }
    }
}
