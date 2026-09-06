using System.Security.Claims;
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

    [Theory(DisplayName = "KyrolusCorrelationMiddleware: Sanitizes malicious correlation IDs and generates fallback")]
    [InlineData("bad\r\nSet-Cookie: evil=true")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("has spaces in id")]
    [InlineData("id_with_special_characters!@#$%^&*()")]
    [InlineData("this_correlation_id_is_way_too_long_and_exceeds_the_sixty_four_character_limit_so_it_must_be_rejected")]
    public async Task CorrelationMiddleware_SanitizesMaliciousCorrelationId(string maliciousId)
    {
        var options = Options.Create(new KyrolusCorrelationOptions());
        string? capturedAmbientCorrelationId = null;

        RequestDelegate next = ctx =>
        {
            capturedAmbientCorrelationId = KyrolusCorrelationContext.CorrelationId;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusCorrelationMiddleware(next, options);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = maliciousId;

        await middleware.InvokeAsync(context);

        var resolvedId = context.Items["Kyrolus_CorrelationId"]?.ToString();
        resolvedId.ShouldNotBeNullOrWhiteSpace();
        resolvedId.ShouldNotBe(maliciousId);
        resolvedId.ShouldNotContain("\r");
        resolvedId.ShouldNotContain("\n");
        resolvedId.ShouldNotContain("<script>");
        capturedAmbientCorrelationId.ShouldBe(resolvedId);
    }

    [Fact(DisplayName = "KyrolusCorrelationMiddleware: Enforces Tenant ID priority (Ambient > Claim > Header)")]
    public async Task CorrelationMiddleware_EnforcesTenantPriority_AndValidatesFormat()
    {
        var options = Options.Create(new KyrolusCorrelationOptions());
        string? capturedTenantId = null;

        RequestDelegate next = _ =>
        {
            capturedTenantId = KyrolusCorrelationContext.TenantId;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusCorrelationMiddleware(next, options);

        // 1. Ambient KyrolusTenantId wins over claim and header
        var ambientContext = new DefaultHttpContext();
        ambientContext.Items["KyrolusTenantId"] = "ambient-tenant-100";
        ambientContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", "claim-tenant-200")], "TestAuth"));
        ambientContext.Request.Headers["X-Tenant-ID"] = "header-tenant-300";

        await middleware.InvokeAsync(ambientContext);
        capturedTenantId.ShouldBe("ambient-tenant-100");

        // 2. Claim wins over header when ambient is absent
        var claimContext = new DefaultHttpContext();
        claimContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", "claim-tenant-200")], "TestAuth"));
        claimContext.Request.Headers["X-Tenant-ID"] = "header-tenant-300";

        await middleware.InvokeAsync(claimContext);
        capturedTenantId.ShouldBe("claim-tenant-200");

        // 3. Valid Header is used when ambient and claim are absent
        var headerContext = new DefaultHttpContext();
        headerContext.Request.Headers["X-Tenant-ID"] = "header-tenant-300";

        await middleware.InvokeAsync(headerContext);
        capturedTenantId.ShouldBe("header-tenant-300");

        // 4. Malicious Header (SQL injection / path traversal) is rejected
        var maliciousHeaderContext = new DefaultHttpContext();
        maliciousHeaderContext.Request.Headers["X-Tenant-ID"] = "tenant' OR 1=1--";

        await middleware.InvokeAsync(maliciousHeaderContext);
        capturedTenantId.ShouldBeNull();
    }
}
