using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KyrolusSous.Logging.UnitTests;

public sealed class CorrelationAndHttpMiddlewareTests
{
    private sealed class CapturingCategoryLogger<T> : IKyrolusLogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?>? Properties)> Entries { get; } = [];

        public bool IsEnabled(LogLevel level) => true;

        public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> values) => null;

        public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        {
            Entries.Add((level, message, exception, properties));
        }
    }

    [Fact(DisplayName = "KyrolusCorrelationContext: Ambient scope sets and restores values correctly")]
    public void CorrelationContext_PushesAndRestores()
    {
        KyrolusCorrelationContext.CorrelationId = "initial_corr";
        KyrolusCorrelationContext.TenantId = "tenant_1";
        KyrolusCorrelationContext.UserId = "user_1";

        using (KyrolusCorrelationContext.BeginScope("scoped_corr", "tenant_2", "user_2"))
        {
            KyrolusCorrelationContext.CorrelationId.ShouldBe("scoped_corr");
            KyrolusCorrelationContext.TenantId.ShouldBe("tenant_2");
            KyrolusCorrelationContext.UserId.ShouldBe("user_2");
        }

        KyrolusCorrelationContext.CorrelationId.ShouldBe("initial_corr");
        KyrolusCorrelationContext.TenantId.ShouldBe("tenant_1");
        KyrolusCorrelationContext.UserId.ShouldBe("user_1");
    }

    [Fact(DisplayName = "KyrolusHttpLoggingMiddleware: Logs successful request and propagates correlation header")]
    public async Task HttpLoggingMiddleware_LogsRequest_Successfully()
    {
        var logger = new CapturingCategoryLogger<KyrolusHttpLoggingMiddleware>();
        var masker = new KyrolusSensitiveDataMasker();
        var options = Options.Create(new KyrolusHttpLoggingOptions
        {
            IncludeRequestBody = true,
            IncludeResponseBody = true
        });

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return ctx.Response.WriteAsync("{\"status\":\"ok\"}");
        };

        var middleware = new KyrolusHttpLoggingMiddleware(next, logger, masker, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/orders";
        context.Request.Method = "POST";
        context.Request.Headers["X-Correlation-ID"] = "corr-test-123";
        context.Request.Headers["X-Tenant-ID"] = "tenant-test-456";

        var requestBodyBytes = Encoding.UTF8.GetBytes("{\"amount\":100}");
        context.Request.Body = new MemoryStream(requestBodyBytes);
        context.Request.ContentLength = requestBodyBytes.Length;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Correlation-ID").ShouldBeTrue();
        context.Response.Headers["X-Correlation-ID"].ToString().ShouldBe("corr-test-123");

        logger.Entries.Count.ShouldBe(1);
        var entry = logger.Entries[0];
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties.ShouldNotBeNull();
        entry.Properties["HttpMethod"].ShouldBe("POST");
        entry.Properties["HttpPath"].ShouldBe("/api/v1/orders");
        entry.Properties["HttpStatusCode"].ShouldBe(200);
        entry.Properties["CorrelationId"].ShouldBe("corr-test-123");
        entry.Properties["TenantId"].ShouldBe("tenant-test-456");
        entry.Properties["RequestBody"].ShouldBe("{\"amount\":100}");
        entry.Properties["ResponseBody"].ShouldBe("{\"status\":\"ok\"}");
    }

    [Fact(DisplayName = "KyrolusHttpLoggingMiddleware: Skips excluded paths")]
    public async Task HttpLoggingMiddleware_SkipsExcludedPaths()
    {
        var logger = new CapturingCategoryLogger<KyrolusHttpLoggingMiddleware>();
        var masker = new KyrolusSensitiveDataMasker();
        var options = Options.Create(new KyrolusHttpLoggingOptions());

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new KyrolusHttpLoggingMiddleware(next, logger, masker, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context);

        nextCalled.ShouldBeTrue();
        logger.Entries.ShouldBeEmpty(); // Excluded path should not write log
    }

    [Fact(DisplayName = "KyrolusHttpLoggingMiddleware: Logs failed request with error level")]
    public async Task HttpLoggingMiddleware_LogsFailedRequest()
    {
        var logger = new CapturingCategoryLogger<KyrolusHttpLoggingMiddleware>();
        var masker = new KyrolusSensitiveDataMasker();
        var options = Options.Create(new KyrolusHttpLoggingOptions());

        RequestDelegate next = _ => throw new InvalidOperationException("Simulated error");

        var middleware = new KyrolusHttpLoggingMiddleware(next, logger, masker, options);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/failing";
        context.Request.Method = "GET";

        await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        logger.Entries.Count.ShouldBe(1);
        var entry = logger.Entries[0];
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldNotBeNull();
        entry.Properties.ShouldNotBeNull();
        entry.Properties["HttpStatusCode"].ShouldBe(500);
    }
}
