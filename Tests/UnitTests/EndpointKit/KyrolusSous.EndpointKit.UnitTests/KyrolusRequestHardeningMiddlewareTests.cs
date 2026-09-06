using System.Text;
using KyrolusSous.EndpointKit.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusRequestHardeningMiddlewareTests
{
    [Fact(DisplayName = "RequestHardeningMiddleware: Blocks path traversal with 400 ProblemDetails")]
    public async Task RequestHardening_BlocksPathTraversal()
    {
        var invoked = false;
        RequestDelegate next = ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/files/..%2f..%2fetc/passwd";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.ShouldContain("Path contains invalid traversal or control characters.");
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Blocks null-byte injection with 400 ProblemDetails")]
    public async Task RequestHardening_BlocksNullByteInjection()
    {
        var invoked = false;
        RequestDelegate next = ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString("/api/download/avatar.png%00");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Blocks method override on safe HTTP verbs with 405")]
    public async Task RequestHardening_BlocksMethodOverride_OnSafeVerbs()
    {
        var invoked = false;
        RequestDelegate next = ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Headers["X-HTTP-Method-Override"] = "DELETE";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status405MethodNotAllowed);
        context.Response.ContentType.ShouldBe("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.ShouldContain("HTTP method override is not allowed for safe HTTP verbs.");
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Strips untrusted client certificate headers")]
    public async Task RequestHardening_StripsUntrustedClientCertHeaders()
    {
        var invoked = false;
        RequestDelegate next = ctx =>
        {
            invoked = true;
            ctx.Request.Headers.ContainsKey("X-Client-Cert").ShouldBeFalse();
            ctx.Request.Headers.ContainsKey("X-Client-Cert-Subject").ShouldBeFalse();
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Headers["X-Client-Cert"] = "FakeCertificateData";
        context.Request.Headers["X-Client-Cert-Subject"] = "CN=Admin";

        await middleware.InvokeAsync(context);

        invoked.ShouldBeTrue();
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Enforces header count limits with 431")]
    public async Task RequestHardening_EnforcesHeaderCountLimits()
    {
        var invoked = false;
        RequestDelegate next = ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(new KyrolusRequestHardeningOptions
        {
            MaxHeaderCount = 5
        });

        var middleware = new KyrolusRequestHardeningMiddleware(next, options);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        for (var i = 0; i < 10; i++)
        {
            context.Request.Headers[$"X-Dummy-{i}"] = "value";
        }

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status431RequestHeaderFieldsTooLarge);
        context.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Enforces header total size limits with 431")]
    public async Task RequestHardening_EnforcesHeaderTotalSizeLimits()
    {
        var invoked = false;
        RequestDelegate next = ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(new KyrolusRequestHardeningOptions
        {
            MaxTotalHeaderSizeBytes = 100
        });

        var middleware = new KyrolusRequestHardeningMiddleware(next, options);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Large-Header"] = new string('A', 200);

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status431RequestHeaderFieldsTooLarge);
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Allows valid clean requests through")]
    public async Task RequestHardening_AllowsValidRequests()
    {
        var invoked = false;
        RequestDelegate next = ctx =>
        {
            invoked = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/orders/create";
        context.Request.Headers["Content-Type"] = "application/json";

        await middleware.InvokeAsync(context);

        invoked.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }
}
