
namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Helpers;

public class KyrolusExceptionHandlerHelperTests
{
    [Fact(DisplayName = "WriteEnvelopeAsync with KyrolusErrorEnvelope sets status code, content type, and writes JSON")]
    public async Task WriteEnvelopeAsync_With_KyrolusErrorEnvelope_sets_StatusCode_ContentType_WritesJSON()
    {
        //Arrange
        var httpContext = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        httpContext.Response.Body = responseStream;

        var envelope = new KyrolusErrorEnvelope("not_found", "Not Found", "Item not found", "trace-123");

        //Act
        await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(NullLogger.Instance, httpContext, HttpStatusCode.NotFound, envelope);
        //Assert
        httpContext.Response.StatusCode.ShouldBe(404);
        httpContext.Response.ContentType.ShouldBe("application/json");
        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();
        json.ShouldContain("\"code\":\"not_found\"");
        json.ShouldContain("\"title\":\"Not Found\"");
        json.ShouldContain("\"detail\":\"Item not found\"");
        json.ShouldContain("\"traceId\":\"trace-123\"");
    }
    [Fact(DisplayName = "WriteEnvelopeAsync convenience overload creates envelope and writes JSON")]
    public async Task WriteEnvelopeAsync_ConvenienceOverload_WritesJsonCorrectly()
    {
        // 1. Arrange
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-custom-999"
        };
        using var responseStream = new MemoryStream();
        httpContext.Response.Body = responseStream;
        // 2. Act
        await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
            NullLogger.Instance,
            httpContext,
            HttpStatusCode.BadRequest,
            "invalid_input",
            "Invalid Input",
            "Value cannot be negative");
        // 3. Assert
        httpContext.Response.StatusCode.ShouldBe(400);
        httpContext.Response.ContentType.ShouldBe("application/json");
        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();
        json.ShouldContain("\"code\":\"invalid_input\"");
        json.ShouldContain("\"title\":\"Invalid Input\"");
        json.ShouldContain("\"detail\":\"Value cannot be negative\"");
        json.ShouldContain("\"traceId\":\"trace-custom-999\"");
    }
    [Fact(DisplayName = "WriteEnvelopeAsync logs error with correct level, code, status, path, and message")]
    public async Task WriteEnvelopeAsync_LogsErrorCorrectly()
    {
        // 1. Arrange
        var logger = new TestLogger();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users/42";
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var envelope = new KyrolusErrorEnvelope(
            "user_not_found",
            "Not Found",
            "User 42 does not exist",
            "trace-123");

        // 2. Act
        await KyrolusExceptionHandlerHelper.WriteEnvelopeAsync(
            logger,
            context,
            HttpStatusCode.NotFound,
            envelope);

        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Error);
        logger.Logs[0].Message.ShouldContain("Exception handled: user_not_found (404)");
        logger.Logs[0].Message.ShouldContain("Path=/api/users/42");
        logger.Logs[0].Message.ShouldContain("Message=User 42 does not exist");
    }
    
}


