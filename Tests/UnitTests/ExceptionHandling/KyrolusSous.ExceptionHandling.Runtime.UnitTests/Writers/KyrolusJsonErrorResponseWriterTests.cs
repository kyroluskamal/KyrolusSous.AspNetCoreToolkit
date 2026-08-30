using System.IO;
using System.Text;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Writers;

public class KyrolusJsonErrorResponseWriterTests
{
    private static readonly KyrolusErrorContext TestErrorContext = new(
        TraceId: "trace-test-123",
        CorrelationId: "corr-456",
        UserId: "user-789",
        TenantId: "tenant-101",
        Path: "/api/products",
        Method: "POST",
        Culture: null);

    [Fact(DisplayName = "KyrolusJsonErrorResponseWriter should set ContentType and StatusCode on HttpContext response")]
    public async Task WriteAsync_Should_Set_ContentType_And_StatusCode()
    {
        var writer = new KyrolusJsonErrorResponseWriter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var mapping = KyrolusExceptionMapping.Create(
            code: "bad_request",
            title: "Bad Request",
            statusCode: HttpStatusCode.BadRequest,
            detail: "Invalid input",
            traceId: "trace-test-123");

        await writer.WriteAsync(httpContext, mapping, TestErrorContext, CancellationToken.None);

        httpContext.Response.ContentType.ShouldBe("application/json");
        httpContext.Response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "KyrolusJsonErrorResponseWriter should write serialized envelope JSON to response body")]
    public async Task WriteAsync_Should_Write_Serialized_Envelope_To_Body()
    {
        var writer = new KyrolusJsonErrorResponseWriter();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var errors = new List<KyrolusErrorItem>
        {
            new("Email", "invalid_email", "Email format is incorrect")
        };
        var metadata = new Dictionary<string, object?>
        {
            ["attempt"] = 3,
            ["ipAddress"] = "127.0.0.1"
        };

        var mapping = KyrolusExceptionMapping.Create(
            code: "validation_failed",
            title: "Validation Failed",
            statusCode: HttpStatusCode.UnprocessableEntity,
            detail: "One or more validation rules failed",
            traceId: "trace-test-123",
            errors: errors,
            metadata: metadata);

        await writer.WriteAsync(httpContext, mapping, TestErrorContext, CancellationToken.None);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();

        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"code\":\"validation_failed\"");
        json.ShouldContain("\"title\":\"Validation Failed\"");
        json.ShouldContain("\"detail\":\"One or more validation rules failed\"");
        json.ShouldContain("\"traceId\":\"trace-test-123\"");
        json.ShouldContain("\"field\":\"Email\"");
        json.ShouldContain("\"code\":\"invalid_email\"");
        json.ShouldContain("\"attempt\":3");
        json.ShouldContain("\"ipAddress\":\"127.0.0.1\"");
    }

    [Fact(DisplayName = "KyrolusExceptionJsonContext should have generated JsonTypeInfo for all core models")]
    public void JsonContext_Should_Have_Generated_TypeInfo_For_Core_Models()
    {
        KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope.ShouldNotBeNull();
        KyrolusExceptionJsonContext.Default.KyrolusErrorItem.ShouldNotBeNull();
        KyrolusExceptionJsonContext.Default.KyrolusErrorContextInfo.ShouldNotBeNull();

        KyrolusExceptionJsonContext.Default.GetTypeInfo(typeof(KyrolusErrorEnvelope)).ShouldNotBeNull();
        KyrolusExceptionJsonContext.Default.GetTypeInfo(typeof(KyrolusErrorItem)).ShouldNotBeNull();
        KyrolusExceptionJsonContext.Default.GetTypeInfo(typeof(KyrolusErrorContextInfo)).ShouldNotBeNull();
        KyrolusExceptionJsonContext.Default.GetTypeInfo(typeof(Dictionary<string, object?>)).ShouldNotBeNull();
        KyrolusExceptionJsonContext.Default.GetTypeInfo(typeof(IReadOnlyDictionary<string, object?>)).ShouldNotBeNull();
    }

    [Fact(DisplayName = "KyrolusExceptionJsonContext should serialize and deserialize complete envelope with metadata correctly")]
    public void JsonContext_Should_Serialize_And_Deserialize_Complete_Envelope()
    {
        var original = new KyrolusErrorEnvelope(
            Code: "order_error",
            Title: "Order Error",
            Detail: "Order could not be processed",
            TraceId: "trace-xyz-789",
            Errors:
            [
                new KyrolusErrorItem("Amount", "insufficient_funds", "Account has insufficient funds")
            ],
            Metadata: new Dictionary<string, object?>
            {
                ["orderId"] = "ORD-999",
                ["retryCount"] = 2,
                ["isCritical"] = true
            });

        var json = JsonSerializer.Serialize(original, KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope);

        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"code\":\"order_error\"");
        json.ShouldContain("\"insufficient_funds\"");

        var deserialized = JsonSerializer.Deserialize(json, KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope);

        deserialized.ShouldNotBeNull();
        deserialized.Code.ShouldBe("order_error");
        deserialized.Title.ShouldBe("Order Error");
        deserialized.Detail.ShouldBe("Order could not be processed");
        deserialized.TraceId.ShouldBe("trace-xyz-789");
        deserialized.Errors.ShouldNotBeNull();
        deserialized.Errors.Count.ShouldBe(1);
        deserialized.Errors[0].Field.ShouldBe("Amount");
        deserialized.Errors[0].Code.ShouldBe("insufficient_funds");
        deserialized.Metadata.ShouldNotBeNull();
        deserialized.Metadata.ShouldContainKey("orderId");
        deserialized.Metadata["orderId"]!.ToString().ShouldBe("ORD-999");
    }

    [Fact(DisplayName = "KyrolusExceptionJsonContext should serialize KyrolusErrorContextInfo correctly")]
    public void JsonContext_Should_Serialize_KyrolusErrorContextInfo_Correctly()
    {
        var contextInfo = new KyrolusErrorContextInfo
        {
            RequestPath = "/api/test",
            HttpMethod = "GET",
            Controller = "Products",
            Action = "GetById",
            EndpointName = "GetProductById"
        };

        var json = JsonSerializer.Serialize(contextInfo, KyrolusExceptionJsonContext.Default.KyrolusErrorContextInfo);

        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("\"requestPath\":\"/api/test\"");
        json.ShouldContain("\"httpMethod\":\"GET\"");
        json.ShouldContain("\"controller\":\"Products\"");
        json.ShouldContain("\"action\":\"GetById\"");
    }
}
