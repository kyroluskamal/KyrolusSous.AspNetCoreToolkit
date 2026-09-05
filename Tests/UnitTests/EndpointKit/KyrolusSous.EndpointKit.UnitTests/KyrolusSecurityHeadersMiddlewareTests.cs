using KyrolusSous.EndpointKit.Core.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusSecurityHeadersMiddlewareTests
{
    [Fact(DisplayName = "KyrolusSecurityHeadersMiddleware: Applies default hardened security headers")]
    public async Task SecurityHeadersMiddleware_AppliesDefaultHeaders()
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusSecurityHeadersMiddleware(next);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().ShouldBe("DENY");
        context.Response.Headers["X-XSS-Protection"].ToString().ShouldBe("1; mode=block");
        context.Response.Headers["Referrer-Policy"].ToString().ShouldBe("strict-origin-when-cross-origin");
        context.Response.Headers.ContainsKey("Content-Security-Policy").ShouldBeFalse();
        context.Response.Headers.ContainsKey("Permissions-Policy").ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusSecurityHeadersMiddleware: Respects custom configured options")]
    public async Task SecurityHeadersMiddleware_RespectsCustomOptions()
    {
        var options = Options.Create(new KyrolusSecurityHeadersOptions
        {
            FrameOptions = "SAMEORIGIN",
            ContentSecurityPolicy = "default-src 'self'",
            PermissionsPolicy = "geolocation=()",
            XssProtection = null // Suppressed
        });

        RequestDelegate next = ctx => Task.CompletedTask;

        var middleware = new KyrolusSecurityHeadersMiddleware(next, options);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Frame-Options"].ToString().ShouldBe("SAMEORIGIN");
        context.Response.Headers["Content-Security-Policy"].ToString().ShouldBe("default-src 'self'");
        context.Response.Headers["Permissions-Policy"].ToString().ShouldBe("geolocation=()");
        context.Response.Headers.ContainsKey("X-XSS-Protection").ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusSecurityHeadersMiddleware: Does not overwrite pre-existing header")]
    public async Task SecurityHeadersMiddleware_DoesNotOverwritePreExistingHeader()
    {
        RequestDelegate next = ctx => Task.CompletedTask;

        var middleware = new KyrolusSecurityHeadersMiddleware(next);
        var context = new DefaultHttpContext();
        context.Response.Headers["X-Frame-Options"] = "ALLOW-FROM https://trusted.com";

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Frame-Options"].ToString().ShouldBe("ALLOW-FROM https://trusted.com");
    }

    [Fact(DisplayName = "SecurityHeadersApplicationBuilderExtensions: DI and pipeline registration works")]
    public void SecurityHeaders_DI_And_PipelineRegistration_Works()
    {
        var services = new ServiceCollection();
        services.AddKyrolusSecurityHeaders(opt =>
        {
            opt.FrameOptions = "SAMEORIGIN";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KyrolusSecurityHeadersOptions>>().Value;

        options.FrameOptions.ShouldBe("SAMEORIGIN");
        options.ContentTypeOptions.ShouldBe("nosniff");
    }
}
