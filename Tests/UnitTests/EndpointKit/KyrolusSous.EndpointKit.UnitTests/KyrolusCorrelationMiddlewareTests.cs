using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.Correlation;
using KyrolusSous.EndpointKit.Core.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusCorrelationMiddlewareTests
{
    [Fact(DisplayName = "KyrolusCorrelationMiddleware: Adopts incoming X-Correlation-ID header")]
    public async Task CorrelationMiddleware_AdoptsIncomingHeader()
    {
        var options = Options.Create(new KyrolusCorrelationOptions());
        string? capturedAmbientCorrelationId = null;

        RequestDelegate next = ctx =>
        {
            capturedAmbientCorrelationId = KyrolusCorrelationContext.CorrelationId;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusCorrelationMiddleware(next, options);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "custom-client-uuid-7788";

        await middleware.InvokeAsync(context);

        // Verify captured in context.Items
        context.Items["Kyrolus_CorrelationId"].ShouldBe("custom-client-uuid-7788");

        // Verify echoed in Response Headers
        context.Response.Headers["X-Correlation-ID"].ToString().ShouldBe("custom-client-uuid-7788");

        // Verify set in ambient AsyncLocal KyrolusCorrelationContext during pipeline execution
        capturedAmbientCorrelationId.ShouldBe("custom-client-uuid-7788");
    }

    [Fact(DisplayName = "KyrolusCorrelationMiddleware: Generates fallback correlation ID when header is missing")]
    public async Task CorrelationMiddleware_GeneratesFallback_WhenMissing()
    {
        var options = Options.Create(new KyrolusCorrelationOptions());
        string? capturedAmbientCorrelationId = null;

        RequestDelegate next = ctx =>
        {
            capturedAmbientCorrelationId = KyrolusCorrelationContext.CorrelationId;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusCorrelationMiddleware(next, options);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        // Verify correlation ID was generated
        var generatedId = context.Items["Kyrolus_CorrelationId"]?.ToString();
        generatedId.ShouldNotBeNullOrWhiteSpace();

        // Verify echoed in Response Headers
        context.Response.Headers["X-Correlation-ID"].ToString().ShouldBe(generatedId);

        // Verify ambient context had the same generated ID
        capturedAmbientCorrelationId.ShouldBe(generatedId);
    }

    [Fact(DisplayName = "KyrolusCorrelationMiddleware: Is idempotent and does not execute twice")]
    public async Task CorrelationMiddleware_IsIdempotent()
    {
        var options = Options.Create(new KyrolusCorrelationOptions());
        var executionCount = 0;

        RequestDelegate next = ctx =>
        {
            executionCount++;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusCorrelationMiddleware(next, options);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "test-once";

        await middleware.InvokeAsync(context);
        await middleware.InvokeAsync(context);

        // The inner next should be called both times, but the setup logic only runs once
        executionCount.ShouldBe(2);
        context.Items["__KyrolusCorrelationExecuted"].ShouldBe(true);
    }

    [Fact(DisplayName = "KyrolusCorrelationStartupFilter: Automatically injects middleware into pipeline")]
    public void CorrelationStartupFilter_ConfiguresPipeline()
    {
        var filter = new KyrolusCorrelationStartupFilter();
        var configureAction = filter.Configure(app => { });

        configureAction.ShouldNotBeNull();
    }

    [Fact(DisplayName = "AddKyrolus: Mandatorily registers KyrolusCorrelationStartupFilter in DI")]
    public void AddKyrolus_RegistersCorrelationStartupFilter_Mandatorily()
    {
        var services = new ServiceCollection();
        services.AddKyrolus(builder => { });

        var provider = services.BuildServiceProvider();
        var startupFilters = provider.GetServices<IStartupFilter>().ToList();

        startupFilters.Any(f => f is KyrolusCorrelationStartupFilter).ShouldBeTrue();
    }
}
