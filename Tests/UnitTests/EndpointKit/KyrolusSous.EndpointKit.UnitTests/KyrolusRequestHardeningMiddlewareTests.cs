using System.Net;
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
        body.ShouldContain("Path traversal or invalid characters detected in the request path or query.");
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

    [Fact(DisplayName = "RequestHardeningMiddleware: Blocks conflicting Transfer-Encoding and Content-Length with 400")]
    public async Task RequestHardening_BlocksConflicting_TransferEncoding_And_ContentLength()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Transfer-Encoding"] = "chunked";
        context.Request.Headers["Content-Length"] = "42";

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.ShouldContain("Conflicting or duplicate content transfer headers detected");
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Blocks multiple differing Content-Length headers with 400")]
    public async Task RequestHardening_BlocksMultipleDifferingContentLengthHeaders()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Append("Content-Length", "42");
        context.Request.Headers.Append("Content-Length", "99");

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Blocks Transfer-Encoding with control characters with 400")]
    public async Task RequestHardening_BlocksTransferEncodingWithControlChars()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["Transfer-Encoding"] = "chunked\r\ninvalid";

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Blocks path traversal in query string with 400")]
    public async Task RequestHardening_BlocksPathTraversalInQueryString()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/files/view";
        context.Request.QueryString = new QueryString("?filename=..%2f..%2fetc%2fpasswd");

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Enforces MaxRequestBodySizeBytes with 413")]
    public async Task RequestHardening_EnforcesMaxRequestBodySizeBytes()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(new KyrolusRequestHardeningOptions
        {
            MaxRequestBodySizeBytes = 1024 // 1 KB limit
        });

        var middleware = new KyrolusRequestHardeningMiddleware(next, options);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.ContentLength = 2048; // Exceeds limit

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        context.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Theory(DisplayName = "RequestHardeningMiddleware: Blocks dangerous verbs TRACE, TRACK, CONNECT with 405")]
    [InlineData("TRACE")]
    [InlineData("TRACK")]
    [InlineData("CONNECT")]
    public async Task RequestHardening_BlocksDangerousVerbs(string verb)
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = verb;

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status405MethodNotAllowed);
        context.Response.ContentType.ShouldBe("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.ShouldContain("https://httpstatuses.com/405");
        body.ShouldContain("Method Not Allowed");
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Rejects untrusted Host header when AllowedHosts is configured")]
    public async Task RequestHardening_Enforces_AllowedHosts()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(new KyrolusRequestHardeningOptions
        {
            AllowedHosts = ["api.example.com", "*.corp.internal"]
        });

        var middleware = new KyrolusRequestHardeningMiddleware(next, options);

        // 1. Untrusted attacker host
        var untrustedContext = new DefaultHttpContext();
        untrustedContext.Response.Body = new MemoryStream();
        untrustedContext.Request.Host = new HostString("attacker.evil.com");

        await middleware.InvokeAsync(untrustedContext);

        invoked.ShouldBeFalse();
        untrustedContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        untrustedContext.Response.ContentType.ShouldBe("application/problem+json");

        // 2. Exact match allowed host
        var exactContext = new DefaultHttpContext();
        exactContext.Request.Host = new HostString("api.example.com");

        await middleware.InvokeAsync(exactContext);
        invoked.ShouldBeTrue();

        // 3. Wildcard subdomain allowed host
        invoked = false;
        var wildcardContext = new DefaultHttpContext();
        wildcardContext.Request.Host = new HostString("payments.corp.internal");

        await middleware.InvokeAsync(wildcardContext);
        invoked.ShouldBeTrue();
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Enforces IP allowlist and blocklist")]
    public async Task RequestHardening_Enforces_IpAllowlist_And_Blocklist()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        // Scenario 1: Blocked IP
        var blockOptions = Options.Create(new KyrolusRequestHardeningOptions
        {
            BlockedIpsOrCidrs = ["198.51.100.4", "192.168.1.0/24"]
        });

        var blockMiddleware = new KyrolusRequestHardeningMiddleware(next, blockOptions);
        var blockedContext = new DefaultHttpContext();
        blockedContext.Response.Body = new MemoryStream();
        blockedContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.55");

        await blockMiddleware.InvokeAsync(blockedContext);

        invoked.ShouldBeFalse();
        blockedContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        blockedContext.Response.ContentType.ShouldBe("application/problem+json");

        // Scenario 2: Allowlist only
        var allowOptions = Options.Create(new KyrolusRequestHardeningOptions
        {
            AllowedIpsOrCidrs = ["10.0.0.0/8"]
        });

        var allowMiddleware = new KyrolusRequestHardeningMiddleware(next, allowOptions);

        // Outside allowlist -> rejected
        var nonAllowedContext = new DefaultHttpContext();
        nonAllowedContext.Response.Body = new MemoryStream();
        nonAllowedContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        await allowMiddleware.InvokeAsync(nonAllowedContext);
        invoked.ShouldBeFalse();
        nonAllowedContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);

        // Inside allowlist -> accepted
        var allowedContext = new DefaultHttpContext();
        allowedContext.Connection.RemoteIpAddress = IPAddress.Parse("10.50.1.99");

        await allowMiddleware.InvokeAsync(allowedContext);
        invoked.ShouldBeTrue();
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Enforces AllowedContentTypes with 415")]
    public async Task RequestHardening_Enforces_AllowedContentTypes()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(new KyrolusRequestHardeningOptions
        {
            AllowedContentTypes = ["application/json", "text/plain"]
        });

        var middleware = new KyrolusRequestHardeningMiddleware(next, options);

        // 1. Allowed Content-Type (with charset parameter)
        var allowedContext = new DefaultHttpContext();
        allowedContext.Request.ContentLength = 100;
        allowedContext.Request.ContentType = "application/json; charset=utf-8";

        await middleware.InvokeAsync(allowedContext);
        invoked.ShouldBeTrue();

        // 2. Disallowed Content-Type (e.g. application/xml)
        invoked = false;
        var rejectedContext = new DefaultHttpContext();
        rejectedContext.Response.Body = new MemoryStream();
        rejectedContext.Request.ContentLength = 100;
        rejectedContext.Request.ContentType = "application/xml";

        await middleware.InvokeAsync(rejectedContext);
        invoked.ShouldBeFalse();
        rejectedContext.Response.StatusCode.ShouldBe(StatusCodes.Status415UnsupportedMediaType);
        rejectedContext.Response.ContentType.ShouldBe("application/problem+json");

        rejectedContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(rejectedContext.Response.Body).ReadToEndAsync();
        body.ShouldContain("https://httpstatuses.com/415");

        // 3. Request without body passes through without checking ContentType
        invoked = false;
        var noBodyContext = new DefaultHttpContext();
        noBodyContext.Request.ContentLength = 0;
        noBodyContext.Request.ContentType = "application/xml";

        await middleware.InvokeAsync(noBodyContext);
        invoked.ShouldBeTrue();
    }

    [Theory(DisplayName = "RequestHardeningMiddleware: Blocks matrix parameter and semicolon traversal attempts")]
    [InlineData("/api/files/..;/..;/etc/passwd")]
    [InlineData("/api/files/..%3b/etc/passwd")]
    [InlineData("/api/files/%3b../etc/passwd")]
    [InlineData("/api/.;/test")]
    public async Task RequestHardening_Blocks_MatrixSemicolonTraversal(string path)
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusRequestHardeningMiddleware(next);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        invoked.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");
    }

    [Fact(DisplayName = "RequestHardeningMiddleware: Sets IHttpMaxRequestBodySizeFeature when configured")]
    public async Task RequestHardening_Sets_MaxRequestBodySizeFeature()
    {
        var invoked = false;
        RequestDelegate next = _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        };

        var options = Options.Create(new KyrolusRequestHardeningOptions
        {
            MaxRequestBodySizeBytes = 5000
        });

        var middleware = new KyrolusRequestHardeningMiddleware(next, options);
        var context = new DefaultHttpContext();
        var feature = new TestMaxRequestBodySizeFeature();
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>(feature);

        await middleware.InvokeAsync(context);

        invoked.ShouldBeTrue();
        feature.MaxRequestBodySize.ShouldBe(5000);
    }

    private sealed class TestMaxRequestBodySizeFeature : Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }
}
