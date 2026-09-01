using System.Net.Http;
using System.Text.Json;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Handlers;

public class KyrolusExceptionHandlersTests
{
    private static KyrolusExceptionHandlingDependencies BuildDependencies(
        Action<KyrolusExceptionHandlingOptions>? configureOptions = null,
        IKyrolusLocalizer? localizer = null,
        string environmentName = "Production",
        Action<IServiceCollection>? configureServices = null,
        ILogger<KyrolusExceptionHandlingDependencies>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        services.AddSingleton(logger ?? NullLogger<KyrolusExceptionHandlingDependencies>.Instance);
        if (localizer is not null)
            services.AddSingleton(localizer);

        configureServices?.Invoke(services);
        services.AddKyrolusExceptionHandling(configureOptions);

        return services.BuildServiceProvider().GetRequiredService<KyrolusExceptionHandlingDependencies>();
    }

    private static readonly KyrolusExceptionHandlingDependencies SharedDependencies = BuildDependencies();

    public static TheoryData<IExceptionHandler, Exception, HttpStatusCode, string, string> HandlerTestData =>
        new()
        {
            {
                new JsonExceptionHandler(SharedDependencies),
                new JsonException("Malformed json payload"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.InvalidJson,
                "Invalid JSON payload"
            },
            {
                new TimeoutExceptionHandler(SharedDependencies),
                new TimeoutException("Operation timed out"),
                HttpStatusCode.GatewayTimeout,
                KyrolusErrorCodes.Timeout,
                "Operation timeout"
            },
            {
                new SocketExceptionHandler(SharedDependencies),
                new SocketException((int)SocketError.ConnectionRefused),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "Network connection failed"
            },
            {
                new ArgumentExceptionHandler(SharedDependencies),
                new ArgumentException("Invalid param"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Invalid argument"
            },
            {
                new CultureNotFoundExceptionHandler(SharedDependencies),
                new CultureNotFoundException("xx-YY"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Invalid culture"
            },
            {
                new NotFoundExceptionHandler(SharedDependencies),
                new KyrolusNotFoundException("Product", "123"),
                HttpStatusCode.NotFound,
                KyrolusErrorCodes.NotFound,
                "Product not found"
            },
            {
                new NotFoundExceptionHandler(SharedDependencies),
                new KyrolusNotFoundException("Order", 456),
                HttpStatusCode.NotFound,
                KyrolusErrorCodes.NotFound,
                "Order not found"
            },
            {
                new UnauthorizedExceptionHandler(SharedDependencies),
                new KyrolusUnauthorizedException("Access denied"),
                HttpStatusCode.Unauthorized,
                KyrolusErrorCodes.Unauthorized,
                "Unauthorized"
            },
            {
                new UnauthorizedExceptionHandler(SharedDependencies),
                new KyrolusUnauthorizedException("Access denied", new InvalidOperationException("Token expired")),
                HttpStatusCode.Unauthorized,
                KyrolusErrorCodes.Unauthorized,
                "Unauthorized"
            },
            {
                new HttpRequestExceptionHandler(SharedDependencies),
                new HttpRequestException("Remote service failed"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "Upstream HTTP service error"
            },
            {
                new SslAuthenticationExceptionHandler(SharedDependencies),
                new AuthenticationException("Certificate invalid"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "SSL authentication failed"
            },
            {
                new SslAuthenticationExceptionHandler(SharedDependencies),
                new SslAuthenticationException("SSL failed"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "SSL authentication failed"
            },
            {
                new SslAuthenticationExceptionHandler(SharedDependencies),
                new SslAuthenticationException("SSL handshake failed", new InvalidOperationException("Untrusted root")),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "SSL authentication failed"
            },
            {
                new GeneralExceptionHandler(SharedDependencies),
                new Exception("General error"),
                HttpStatusCode.InternalServerError,
                KyrolusErrorCodes.InternalError,
                "Internal server error"
            }
        };

    public static TheoryData<IExceptionHandler> HandlersList =>
        new()
        {
            new JsonExceptionHandler(SharedDependencies),
            new TimeoutExceptionHandler(SharedDependencies),
            new SocketExceptionHandler(SharedDependencies),
            new ArgumentExceptionHandler(SharedDependencies),
            new CultureNotFoundExceptionHandler(SharedDependencies),
            new NotFoundExceptionHandler(SharedDependencies),
            new UnauthorizedExceptionHandler(SharedDependencies),
            new HttpRequestExceptionHandler(SharedDependencies),
            new SslAuthenticationExceptionHandler(SharedDependencies)
        };

    [Theory(DisplayName = "Handlers should handle their matching exception type and write envelope JSON, all via the shared translation pipeline")]
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

    [Fact(DisplayName = "Handlers should log through the shared logging policy")]
    public async Task Handlers_Should_Log_Error_When_Handling_Exception()
    {
        // TimeoutException maps with ShouldLog: true (per KyrolusFrameworkExceptionMapper) - unlike a routine
        // JsonException (ShouldLog: false), it's expected to actually produce a log entry.
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var dependencies = BuildDependencies(logger: logger);
        var handler = new TimeoutExceptionHandler(dependencies);
        var exception = new TimeoutException("Operation timed out after 30s");

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/data";
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        logger.Logs.Count.ShouldBe(1);
        logger.Logs[0].Level.ShouldBe(LogLevel.Error);
        logger.Logs[0].Message.ShouldContain("timeout");
        logger.Logs[0].Message.ShouldContain("/api/data");
    }

    [Fact(DisplayName = "Handlers should not log exceptions the mapper marks as ShouldLog: false")]
    public async Task Handlers_Should_Not_Log_Routine_Exceptions()
    {
        var logger = new TestLogger<KyrolusExceptionHandlingDependencies>();
        var dependencies = BuildDependencies(logger: logger);
        var handler = new JsonExceptionHandler(dependencies);

        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, new JsonException("Invalid json syntax at line 5"), CancellationToken.None);

        handled.ShouldBeTrue();
        logger.Logs.ShouldBeEmpty();
    }

    [Theory(DisplayName = "GeneralExceptionHandler never leaks the raw message of an unclassified exception, in any environment")]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task GeneralExceptionHandler_Should_HideMessage_InAnyEnvironment(string environmentName)
    {
        var dependencies = BuildDependencies(environmentName: environmentName);
        var handler = new GeneralExceptionHandler(dependencies);

        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("connection string: secret"), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);

        responseStream.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(responseStream).ReadToEndAsync();

        json.ShouldContain("An unexpected error occurred.");
        json.ShouldNotContain("connection string: secret");
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

    [Fact(DisplayName = "A custom exception implementing IKyrolusExceptionWithErrors gets its errors extracted automatically")]
    public async Task Handler_Should_Extract_Errors_From_Exception_With_Errors()
    {
        var handler = new TestCustomValidationExceptionHandler(SharedDependencies);
        var exception = new TestValidationException("Validation failed",
        [
            new KyrolusErrorItem("Email", "invalid_email", "Email format is invalid")
        ]);

        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(500); // unmapped exception type falls through to the default mapper

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var json = await reader.ReadToEndAsync();

        json.ShouldContain("invalid_email");
        json.ShouldContain("Email format is invalid");
    }

    [Fact(DisplayName = "A KyrolusException's own status/code/title/errors are used automatically regardless of which handler caught it")]
    public async Task Handler_Should_Extract_Errors_From_KyrolusException()
    {
        var handler = new GeneralExceptionHandler(SharedDependencies);
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
        var notFound1 = new KyrolusNotFoundException("Order", "1001");
        notFound1.Message.ShouldBe("Order with key '1001' was not found.");
        notFound1.EntityName.ShouldBe("Order");
        notFound1.Key.ShouldBe("1001");

        var unauthorized1 = new KyrolusUnauthorizedException("Unauthorized action");
        unauthorized1.Message.ShouldBe("Unauthorized action");

        var innerUnauth = new InvalidOperationException("Expired");
        var unauthorized2 = new KyrolusUnauthorizedException("Unauthorized action", innerUnauth);
        unauthorized2.Message.ShouldBe("Unauthorized action");
        unauthorized2.InnerException.ShouldBeSameAs(innerUnauth);

        var ssl1 = new SslAuthenticationException("SSL failed");
        ssl1.Message.ShouldBe("SSL failed");

        var innerSsl = new InvalidOperationException("Untrusted certificate");
        var ssl2 = new SslAuthenticationException("SSL handshake failed", innerSsl);
        ssl2.Message.ShouldBe("SSL handshake failed");
        ssl2.InnerException.ShouldBeSameAs(innerSsl);
    }

    [Fact(DisplayName = "A custom IKyrolusExceptionMapper drives status/code/title for any handler, and its metadata is sanitized by default")]
    public async Task Handler_Should_Use_Custom_Mapper_And_Sanitize_Metadata_By_Default()
    {
        var dependencies = BuildDependencies(configureServices: services =>
            services.AddSingleton<IKyrolusExceptionMapper, TestPaymentFailedExceptionMapper>());
        var handler = new GeneralExceptionHandler(dependencies);
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
        json.ShouldNotContain("SuperSecretPassword"); // sanitized by the default, always-registered sanitizer
    }

    [Fact(DisplayName = "Handler should apply localization when IKyrolusLocalizer is registered")]
    public async Task Handler_Should_Apply_Localization_When_Localizer_Is_Provided()
    {
        var translations = new Dictionary<string, string>
        {
            [KyrolusErrorCodes.BadRequest] = "طلب غير صالح",
            [$"{KyrolusErrorCodes.BadRequest}.detail"] = "المعامل المرسل غير صالح"
        };
        var localizer = new TestKeyLookupLocalizer(translations);
        var dependencies = BuildDependencies(localizer: localizer);
        var handler = new ArgumentExceptionHandler(dependencies);
        var exception = new ArgumentException("Invalid param");

        var context = new DefaultHttpContext();
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        handled.ShouldBeTrue();
        responseStream.Seek(0, SeekOrigin.Begin);
        var envelope = await JsonSerializer.DeserializeAsync(responseStream, KyrolusExceptionJsonContext.Default.KyrolusErrorEnvelope);
        envelope.ShouldNotBeNull();
        envelope.Title.ShouldBe("طلب غير صالح");
        envelope.Detail.ShouldBe("المعامل المرسل غير صالح");
    }

    [Fact(DisplayName = "AddKyrolusBuiltInExceptionHandlers should register all 10 built-in exception handlers and the shared translation pipeline")]
    public void AddKyrolusBuiltInExceptionHandlers_Should_Register_All_10_Handlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
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

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<KyrolusExceptionHandlingDependencies>().ShouldNotBeNull();
        provider.GetRequiredService<KyrolusExceptionTranslator>().ShouldNotBeNull();
    }

    private sealed class TestKeyLookupLocalizer(IReadOnlyDictionary<string, string> translations) : IKyrolusLocalizer
    {
        public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null) =>
            translations.TryGetValue(key, out var value)
                ? new KyrolusLocalizationResult(value, ResourceNotFound: false)
                : new KyrolusLocalizationResult(key, ResourceNotFound: true);

        public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null) => GetString(key, culture);

        public string Format(string template, object? arguments) => template;
    }

    private sealed class TestPaymentFailedException(string orderId, string transactionId, string reason)
        : Exception($"Payment failed for order {orderId}: {reason}"), IKyrolusExceptionWithMetadata, IKyrolusExceptionWithErrors
    {
        public IReadOnlyDictionary<string, object?> GetMetadata() => new Dictionary<string, object?>
        {
            ["orderId"] = orderId,
            ["transactionId"] = transactionId,
            ["password"] = "SuperSecretPassword"
        };

        public IReadOnlyList<KyrolusErrorItem>? GetErrors() =>
        [
            new KyrolusErrorItem("Amount", "insufficient_funds", "Account balance is too low")
        ];
    }

    private sealed class TestPaymentFailedExceptionMapper : IKyrolusExceptionMapper
    {
        public int Order => -10;

        public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
        {
            if (exception is not TestPaymentFailedException paymentFailed)
            {
                mapping = null!;
                return false;
            }

            mapping = KyrolusExceptionMapping.Create(
                code: "payment_failed",
                title: "Payment Failed",
                statusCode: HttpStatusCode.PaymentRequired,
                traceId: context.TraceId,
                errors: paymentFailed.GetErrors(),
                metadata: KyrolusMetadataExtractor.Extract(paymentFailed));

            return true;
        }
    }

    private sealed class TestValidationException(string message, IReadOnlyList<KyrolusErrorItem> errors)
        : Exception(message), IKyrolusExceptionWithErrors
    {
        public IReadOnlyList<KyrolusErrorItem>? GetErrors() => errors;
    }

    private sealed class TestCustomValidationExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
        : KyrolusExceptionHandlerBase<TestValidationException>(dependencies);
}
