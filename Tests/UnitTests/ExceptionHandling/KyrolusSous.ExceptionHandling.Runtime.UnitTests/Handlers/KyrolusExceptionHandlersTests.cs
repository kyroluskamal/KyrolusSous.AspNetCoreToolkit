using System.Net.Http;
using System.Text.Json;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Handlers;

public class KyrolusExceptionHandlersTests
{
    public static TheoryData<IExceptionHandler, Exception, HttpStatusCode, string, string> HandlerTestData =>
        new()
        {
            {
                new JsonExceptionHandler(NullLogger<JsonExceptionHandler>.Instance),
                new JsonException("Malformed json payload"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.InvalidJson,
                "Invalid JSON payload"
            },
            {
                new TimeoutExceptionHandler(NullLogger<TimeoutExceptionHandler>.Instance),
                new TimeoutException("Operation timed out"),
                HttpStatusCode.GatewayTimeout,
                KyrolusErrorCodes.Timeout,
                "Request timeout"
            },
            {
                new SocketExceptionHandler(NullLogger<SocketExceptionHandler>.Instance),
                new SocketException((int)SocketError.ConnectionRefused),
                HttpStatusCode.InternalServerError,
                KyrolusErrorCodes.ExternalService,
                "Socket error"
            },
            {
                new ArgumentExceptionHandler(NullLogger<ArgumentExceptionHandler>.Instance),
                new ArgumentException("Invalid param"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Invalid argument"
            },
            {
                new CultureNotFoundExceptionHandler(NullLogger<CultureNotFoundExceptionHandler>.Instance),
                new CultureNotFoundException("xx-YY"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Invalid culture"
            },
            {
                new NotFoundExceptionHandler(NullLogger<NotFoundExceptionHandler>.Instance),
                new NotFoundException("Product", "123"),
                HttpStatusCode.NotFound,
                KyrolusErrorCodes.NotFound,
                "Not found"
            },
            {
                new NotFoundExceptionHandler(NullLogger<NotFoundExceptionHandler>.Instance),
                new NotFoundException("Item not found"),
                HttpStatusCode.NotFound,
                KyrolusErrorCodes.NotFound,
                "Not found"
            },
            {
                new UnauthorizedExceptionHandler(NullLogger<UnauthorizedExceptionHandler>.Instance),
                new UnauthorizedException("Access denied"),
                HttpStatusCode.Unauthorized,
                KyrolusErrorCodes.Unauthorized,
                "Unauthorized"
            },
            {
                new UnauthorizedExceptionHandler(NullLogger<UnauthorizedExceptionHandler>.Instance),
                new UnauthorizedException("Access denied", new InvalidOperationException("Token expired")),
                HttpStatusCode.Unauthorized,
                KyrolusErrorCodes.Unauthorized,
                "Unauthorized"
            },
            {
                new HttpRequestExceptionHandler(NullLogger<HttpRequestExceptionHandler>.Instance),
                new HttpRequestException("Remote service failed"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "External service error"
            },
            {
                new SslAuthenticationExceptionHandler(NullLogger<SslAuthenticationExceptionHandler>.Instance),
                new AuthenticationException("Certificate invalid"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "SSL Authentication failed"
            },
            {
                new SslAuthenticationExceptionHandler(NullLogger<SslAuthenticationExceptionHandler>.Instance),
                new SslAuthenticationException("SSL failed"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "SSL Authentication failed"
            },
            {
                new SslAuthenticationExceptionHandler(NullLogger<SslAuthenticationExceptionHandler>.Instance),
                new SslAuthenticationException("SSL handshake failed", new InvalidOperationException("Untrusted root")),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "SSL Authentication failed"
            },
            {
                new GeneralExceptionHandler(NullLogger<GeneralExceptionHandler>.Instance),
                new Exception("General error"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Bad request"
            }
        };

    public static TheoryData<IExceptionHandler> HandlersList =>
        new()
        {
            new JsonExceptionHandler(NullLogger<JsonExceptionHandler>.Instance),
            new TimeoutExceptionHandler(NullLogger<TimeoutExceptionHandler>.Instance),
            new SocketExceptionHandler(NullLogger<SocketExceptionHandler>.Instance),
            new ArgumentExceptionHandler(NullLogger<ArgumentExceptionHandler>.Instance),
            new CultureNotFoundExceptionHandler(NullLogger<CultureNotFoundExceptionHandler>.Instance),
            new NotFoundExceptionHandler(NullLogger<NotFoundExceptionHandler>.Instance),
            new UnauthorizedExceptionHandler(NullLogger<UnauthorizedExceptionHandler>.Instance),
            new HttpRequestExceptionHandler(NullLogger<HttpRequestExceptionHandler>.Instance),
            new SslAuthenticationExceptionHandler(NullLogger<SslAuthenticationExceptionHandler>.Instance)
        };

    [Theory(DisplayName = "Handlers should handle their matching exception type and write envelope JSON")]
    [MemberData(nameof(HandlerTestData))]
    public async Task Handlers_Should_Handle_Matching_Exception_And_Write_Envelope(
        IExceptionHandler handler,
        Exception exception,
        HttpStatusCode expectedStatusCode,
        string expectedErrorCode,
        string expectedTitle)
    {
        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)expectedStatusCode);
        context.Response.ContentType.ShouldBe("application/json");

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain($"\"{expectedErrorCode}\"");
        json.ShouldContain($"\"{expectedTitle}\"");
    }

    [Fact(DisplayName = "Handlers should log error message and level using provided ILogger")]
    public async Task Handlers_Should_Log_Error_When_Handling_Exception()
    {
        var logger = new TestLogger<JsonExceptionHandler>();
        var handler = new JsonExceptionHandler(logger);
        var exception = new JsonException("Invalid json syntax at line 5");

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/data";
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Error);
        logger.Logs[0].Message.ShouldContain("invalid_json");
        logger.Logs[0].Message.ShouldContain("/api/data");
        logger.Logs[0].Message.ShouldContain("Invalid json syntax at line 5");
    }

    [Theory(DisplayName = "Handlers should return false when receiving unmatched exception type")]
    [MemberData(nameof(HandlersList))]
    public async Task Handlers_Should_Return_False_For_Unmatched_Exceptions(IExceptionHandler handler)
    {
        var unmatchedException = new InvalidOperationException("Unrelated exception");
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, unmatchedException, CancellationToken.None);

        handled.ShouldBeFalse();
    }

    [Fact(DisplayName = "Handler inheriting from KyrolusExceptionHandlerBase should extract custom errors from IKyrolusExceptionWithErrors")]
    public async Task Handler_Should_Extract_Errors_From_Exception_With_Errors()
    {
        var logger = new TestLogger();
        var handler = new TestCustomValidationExceptionHandler(logger);
        var exception = new TestValidationException("Validation failed",
        [
            new KyrolusErrorItem("Email", "invalid_email", "Email format is invalid")
        ]);

        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(400);

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain("invalid_email");
        json.ShouldContain("Email format is invalid");
    }

    [Fact(DisplayName = "Handler inheriting from KyrolusExceptionHandlerBase should extract errors from KyrolusException")]
    public async Task Handler_Should_Extract_Errors_From_KyrolusException()
    {
        var logger = new TestLogger();
        var handler = new TestKyrolusExceptionHandler(logger);
        var exception = new KyrolusValidationException(
        [
            new KyrolusErrorItem("Username", "required", "Username is required")
        ]);

        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(400);

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain("Username");
        json.ShouldContain("required");
        json.ShouldContain("Username is required");
    }

    [Fact(DisplayName = "Custom exception constructors should initialize message and properties correctly")]
    public void CustomExceptions_Constructors_ShouldInitializeCorrectly()
    {
        var notFound1 = new NotFoundException("Custom message");
        notFound1.Message.ShouldBe("Custom message");

        var notFound2 = new NotFoundException("Order", "1001");
        notFound2.Message.ShouldBe("Order with key 1001 not found");

        var unauthorized1 = new UnauthorizedException("Unauthorized action");
        unauthorized1.Message.ShouldBe("Unauthorized action");

        var innerUnauth = new InvalidOperationException("Expired");
        var unauthorized2 = new UnauthorizedException("Unauthorized action", innerUnauth);
        unauthorized2.Message.ShouldBe("Unauthorized action");
        unauthorized2.InnerException.ShouldBeSameAs(innerUnauth);

        var ssl1 = new SslAuthenticationException("SSL failed");
        ssl1.Message.ShouldBe("SSL failed");

        var innerSsl = new InvalidOperationException("Untrusted certificate");
        var ssl2 = new SslAuthenticationException("SSL handshake failed", innerSsl);
        ssl2.Message.ShouldBe("SSL handshake failed");
        ssl2.InnerException.ShouldBeSameAs(innerSsl);
    }

    [Fact(DisplayName = "Handler should handle custom exception with both metadata and errors")]
    public async Task Handler_Should_Handle_Custom_Exception_With_Both_Metadata_And_Errors()
    {
        var logger = new TestLogger();
        var handler = new TestPaymentFailedExceptionHandler(logger);
        var exception = new TestPaymentFailedException("ORD-99", "TX-123", "Account balance is too low");

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/checkout";
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.PaymentRequired);
        context.Response.ContentType.ShouldBe("application/json");

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain("payment_failed");
        json.ShouldContain("Payment Failed");
        json.ShouldContain("ORD-99");
        json.ShouldContain("TX-123");
        json.ShouldContain("insufficient_funds");
        json.ShouldContain("Amount");

        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Error);
        logger.Logs[0].Message.ShouldContain("payment_failed");
        logger.Logs[0].Message.ShouldContain("/api/checkout");
    }

    [Fact(DisplayName = "Handler should use overridden ExtractErrors when provided")]
    public async Task Handler_Should_Use_Overridden_ExtractErrors()
    {
        var logger = new TestLogger();
        var handler = new TestOverriddenExceptionHandler(logger);
        var exception = new InvalidOperationException("Operation failed");

        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(400);

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain("custom_field");
        json.ShouldContain("custom_code");
    }

    [Fact(DisplayName = "AddKyrolusBuiltInExceptionHandlers should register all 10 built-in exception handlers in IServiceCollection")]
    public void AddKyrolusBuiltInExceptionHandlers_Should_Register_All_10_Handlers()
    {
        var services = new ServiceCollection();
        services.AddKyrolusBuiltInExceptionHandlers();

        var handlerDescriptors = services
            .Where(d => d.ServiceType == typeof(IExceptionHandler))
            .Select(d => d.ImplementationType)
            .ToList();

        handlerDescriptors.Count.ShouldBe(10);
        handlerDescriptors.ShouldContain(typeof(CultureNotFoundExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(JsonExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(ArgumentExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(SocketExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(HttpRequestExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(TimeoutExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(NotFoundExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(UnauthorizedExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(SslAuthenticationExceptionHandler));
        handlerDescriptors.ShouldContain(typeof(GeneralExceptionHandler));
    }

    private sealed class TestOverriddenExceptionHandler(ILogger logger)
        : KyrolusExceptionHandlerBase<InvalidOperationException>(
            logger,
            HttpStatusCode.BadRequest,
            KyrolusErrorCodes.BadRequest,
            "Operation error")
    {
        protected override IReadOnlyList<KyrolusErrorItem>? ExtractErrors(InvalidOperationException exception) =>
        [
            new KyrolusErrorItem("custom_field", "custom_code", exception.Message)
        ];
    }

    private sealed class TestPaymentFailedException(string orderId, string transactionId, string reason)
        : Exception($"Payment failed for order {orderId}: {reason}"), IKyrolusExceptionWithMetadata, IKyrolusExceptionWithErrors
    {
        public IReadOnlyDictionary<string, object?> GetMetadata() => new Dictionary<string, object?>
        {
            ["orderId"] = orderId,
            ["transactionId"] = transactionId
        };

        public IReadOnlyList<KyrolusErrorItem>? GetErrors() =>
        [
            new KyrolusErrorItem("Amount", "insufficient_funds", "Account balance is too low")
        ];
    }

    private sealed class TestPaymentFailedExceptionHandler(ILogger logger)
        : KyrolusExceptionHandlerBase<TestPaymentFailedException>(
            logger,
            HttpStatusCode.PaymentRequired,
            "payment_failed",
            "Payment Failed");

    private sealed class TestValidationException(string message, IReadOnlyList<KyrolusErrorItem> errors)
        : Exception(message), IKyrolusExceptionWithErrors
    {
        public IReadOnlyList<KyrolusErrorItem>? GetErrors() => errors;
    }

    private sealed class TestCustomValidationExceptionHandler(ILogger logger)
        : KyrolusExceptionHandlerBase<TestValidationException>(
            logger,
            HttpStatusCode.BadRequest,
            KyrolusErrorCodes.Validation,
            "Validation error");

    private sealed class TestKyrolusExceptionHandler(ILogger logger)
        : KyrolusExceptionHandlerBase<KyrolusValidationException>(
            logger,
            HttpStatusCode.BadRequest,
            KyrolusErrorCodes.Validation,
            "Validation error");
}
