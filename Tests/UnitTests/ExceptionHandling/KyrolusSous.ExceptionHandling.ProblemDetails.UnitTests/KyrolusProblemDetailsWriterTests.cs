using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.ProblemDetails;
using KyrolusSous.ExceptionHandling.Runtime;
using KyrolusSous.ExceptionHandling.Runtime.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.ExceptionHandling.ProblemDetails.UnitTests;

public class KyrolusProblemDetailsWriterTests
{
    private static readonly KyrolusErrorContext TestErrorContext = new(
        TraceId: "trace-pd-123",
        CorrelationId: "corr-456",
        UserId: "user-789",
        TenantId: "tenant-101",
        Path: "/api/orders/checkout",
        Method: "POST",
        Culture: null);

    [Fact(DisplayName = "WriteAsync should set ContentType to application/problem+json and set StatusCode")]
    public async Task WriteAsync_Should_Set_ContentType_And_StatusCode()
    {
        var writer = new KyrolusProblemDetailsWriter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Path = "/api/orders/checkout";

        var mapping = KyrolusExceptionMapping.Create(
            code: "order_conflict",
            title: "Order Conflict",
            statusCode: HttpStatusCode.Conflict,
            detail: "Order version is obsolete",
            traceId: "trace-pd-123");

        await writer.WriteAsync(httpContext, mapping, TestErrorContext, CancellationToken.None);

        httpContext.Response.ContentType.ShouldBe("application/problem+json");
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "WriteAsync should write complete RFC 7807 problem details JSON")]
    public async Task WriteAsync_Should_Write_Complete_ProblemDetails_Json()
    {
        var writer = new KyrolusProblemDetailsWriter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Path = "/api/orders/checkout";

        var errors = new List<KyrolusErrorItem>
        {
            new("ItemCount", "min_items", "Must order at least 1 item")
        };
        var metadata = new Dictionary<string, object?>
        {
            ["cartId"] = "CART-100",
            ["attempt"] = 2
        };

        var mapping = KyrolusExceptionMapping.Create(
            code: "validation_failed",
            title: "Validation Failed",
            statusCode: HttpStatusCode.BadRequest,
            detail: "One or more validation rules failed",
            traceId: "trace-pd-123",
            errors: errors,
            metadata: metadata);

        await writer.WriteAsync(httpContext, mapping, TestErrorContext, CancellationToken.None);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"status\":400");
        json.ShouldContain("\"title\":\"Validation Failed\"");
        json.ShouldContain("\"detail\":\"One or more validation rules failed\"");
        json.ShouldContain("\"type\":\"urn:kyrolus:error:validation_failed\"");
        json.ShouldContain("\"instance\":\"/api/orders/checkout\"");
        json.ShouldContain("\"code\":\"validation_failed\"");
        json.ShouldContain("\"traceId\":\"trace-pd-123\"");
        json.ShouldContain("\"min_items\"");
        json.ShouldContain("\"cartId\":\"CART-100\"");
        json.ShouldContain("\"attempt\":2");
    }

    [Theory(DisplayName = "WriteAsync should build correct ProblemDetails Type URI")]
    [InlineData("", "about:blank")]
    [InlineData("   ", "about:blank")]
    [InlineData(null, "about:blank")]
    [InlineData("https://example.com/errors/not-found", "https://example.com/errors/not-found")]
    [InlineData("custom_error_code", "urn:kyrolus:error:custom_error_code")]
    public async Task WriteAsync_Should_Build_Correct_Problem_Type(string? code, string expectedType)
    {
        var writer = new KyrolusProblemDetailsWriter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var mapping = KyrolusExceptionMapping.Create(
            code: code!,
            title: "Test Error",
            statusCode: HttpStatusCode.BadRequest);

        await writer.WriteAsync(httpContext, mapping, TestErrorContext, CancellationToken.None);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain($"\"type\":\"{expectedType}\"");
    }

    [Fact(DisplayName = "KyrolusProblemDetailsJsonContext should have generated TypeInfo")]
    public void JsonContext_Should_Have_Generated_TypeInfo()
    {
        KyrolusProblemDetailsJsonContext.Default.ProblemDetails.ShouldNotBeNull();
        KyrolusProblemDetailsJsonContext.Default.KyrolusErrorItem.ShouldNotBeNull();
        KyrolusProblemDetailsJsonContext.Default.GetTypeInfo(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails)).ShouldNotBeNull();
    }

    [Fact(DisplayName = "AddKyrolusProblemDetailsWriter should register KyrolusProblemDetailsWriter in DI")]
    public void AddKyrolusProblemDetailsWriter_Should_Replace_ResponseWriter()
    {
        var services = new ServiceCollection();
        services.AddKyrolusExceptionHandling();
        services.AddKyrolusProblemDetailsWriter();

        var provider = services.BuildServiceProvider();
        var writer = provider.GetRequiredService<IKyrolusErrorResponseWriter>();

        writer.ShouldNotBeNull();
        writer.ShouldBeOfType<KyrolusProblemDetailsWriter>();
    }
}
