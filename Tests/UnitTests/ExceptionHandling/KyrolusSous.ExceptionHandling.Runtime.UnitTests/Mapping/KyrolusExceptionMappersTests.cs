using System.Net.Http;
using System.Text.Json;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Mapping;

public class KyrolusExceptionMappersTests
{
    private static readonly KyrolusErrorContext TestContext = new(
        TraceId: "trace-abc-123",
        CorrelationId: "corr-456",
        UserId: "user-789",
        TenantId: "tenant-101",
        Path: "/api/orders",
        Method: "POST",
        Culture: null);

    public static TheoryData<IKyrolusExceptionMapper, Exception, HttpStatusCode, string, string, bool> MapperTestData =>
        new()
        {
            {
                new KyrolusFrameworkExceptionMapper(),
                new JsonException("Malformed JSON syntax"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.InvalidJson,
                "Invalid JSON payload",
                false
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new TimeoutException("Operation timed out"),
                HttpStatusCode.GatewayTimeout,
                KyrolusErrorCodes.Timeout,
                "Operation timeout",
                true
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new SocketException((int)SocketError.ConnectionRefused),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "Network connection failed",
                true
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new HttpRequestException("Gateway timeout", null, HttpStatusCode.GatewayTimeout),
                HttpStatusCode.GatewayTimeout,
                KyrolusErrorCodes.ExternalService,
                "External HTTP request failed",
                true
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new HttpRequestException("Downstream network issue"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "Upstream HTTP service error",
                true
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new UnauthorizedAccessException("Access denied"),
                HttpStatusCode.Unauthorized,
                KyrolusErrorCodes.Unauthorized,
                "Unauthorized access",
                false
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new AuthenticationException("Certificate untrusted"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "SSL authentication failed",
                true
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new KeyNotFoundException("Resource key was not found"),
                HttpStatusCode.NotFound,
                KyrolusErrorCodes.NotFound,
                "Resource key not found",
                false
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new TaskCanceledException("Task was cancelled"),
                HttpStatusCode.RequestTimeout,
                KyrolusErrorCodes.Cancelled,
                "Request cancelled",
                true
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new OperationCanceledException("Operation was cancelled"),
                HttpStatusCode.RequestTimeout,
                KyrolusErrorCodes.Cancelled,
                "Operation cancelled",
                true
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new CultureNotFoundException("xx-INVALID"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Invalid culture",
                false
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new ArgumentException("Invalid argument value"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Invalid argument",
                false
            },
            {
                new KyrolusFrameworkExceptionMapper(),
                new NotSupportedException("Feature not supported"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Operation not supported",
                false
            },
            {
                new KyrolusDomainExceptionMapper(),
                new KyrolusDomainException(HttpStatusCode.Conflict, "item_locked", "Item Locked", "The item is currently locked"),
                HttpStatusCode.Conflict,
                "item_locked",
                "Item Locked",
                false
            },
            {
                new KyrolusDefaultExceptionMapper(),
                new InvalidOperationException("Unhandled unexpected failure"),
                HttpStatusCode.InternalServerError,
                KyrolusErrorCodes.InternalError,
                "Internal server error",
                false
            }
        };

    public static TheoryData<IKyrolusExceptionMapper> MappersList =>
        new()
        {
            new KyrolusFrameworkExceptionMapper(),
            new KyrolusDomainExceptionMapper()
        };

    [Theory(DisplayName = "Mappers should correctly map matching exceptions to KyrolusExceptionMapping")]
    [MemberData(nameof(MapperTestData))]
    public void Mappers_Should_Correctly_Map_Exceptions(
        IKyrolusExceptionMapper mapper,
        Exception exception,
        HttpStatusCode expectedStatusCode,
        string expectedCode,
        string expectedTitle,
        bool expectedIsTransient)
    {
        var handled = mapper.TryMap(exception, TestContext, out var mapping);

        handled.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(expectedStatusCode);
        mapping.Error.Code.ShouldBe(expectedCode);
        mapping.Error.Title.ShouldBe(expectedTitle);
        mapping.Error.TraceId.ShouldBe(TestContext.TraceId);
        mapping.IsTransient.ShouldBe(expectedIsTransient);
    }

    [Theory(DisplayName = "Mappers should return false for unmatched exception types")]
    [MemberData(nameof(MappersList))]
    public void Mappers_Should_Return_False_For_Unmatched_Exceptions(IKyrolusExceptionMapper mapper)
    {
        var unmatchedException = new DivideByZeroException("Unrelated math error");

        var handled = mapper.TryMap(unmatchedException, TestContext, out var mapping);

        handled.ShouldBeFalse();
        mapping.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusDomainExceptionMapper should map domain exception errors and metadata")]
    public void KyrolusDomainExceptionMapper_Should_Map_Errors_And_Metadata()
    {
        var mapper = new KyrolusDomainExceptionMapper();
        var errors = new List<KyrolusErrorItem>
        {
            new("Email", "invalid_email", "Email format is incorrect")
        };
        var metadata = new Dictionary<string, object?> { ["attempt"] = 3 };

        var domainException = new KyrolusDomainException(
            statusCode: HttpStatusCode.UnprocessableEntity,
            code: "validation_failed",
            title: "Validation Failed",
            detail: "One or more fields failed validation",
            errors: errors,
            metadata: metadata,
            isTransient: false,
            shouldLog: true);

        var handled = mapper.TryMap(domainException, TestContext, out var mapping);

        handled.ShouldBeTrue();
        mapping.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        mapping.Error.Errors.ShouldNotBeNull();
        mapping.Error.Errors.Count.ShouldBe(1);
        mapping.Error.Errors[0].Field.ShouldBe("Email");
        mapping.Error.Metadata.ShouldNotBeNull();
        mapping.Error.Metadata["attempt"].ShouldBe(3);
    }

    [Fact(DisplayName = "KyrolusFrameworkExceptionMapper should extract errors from IKyrolusExceptionWithErrors")]
    public void KyrolusFrameworkExceptionMapper_Should_Extract_Errors_From_Interface()
    {
        var mapper = new KyrolusFrameworkExceptionMapper();
        var exception = new CustomArgumentWithErrorsException("Invalid param",
        [
            new KyrolusErrorItem("Age", "min_age", "Age must be >= 18")
        ]);

        var handled = mapper.TryMap(exception, TestContext, out var mapping);

        handled.ShouldBeTrue();
        mapping.Error.Errors.ShouldNotBeNull();
        mapping.Error.Errors.Count.ShouldBe(1);
        mapping.Error.Errors[0].Code.ShouldBe("min_age");
    }

    [Fact(DisplayName = "KyrolusDefaultExceptionMapper should extract errors and metadata")]
    public void KyrolusDefaultExceptionMapper_Should_Extract_Errors_And_Metadata()
    {
        var mapper = new KyrolusDefaultExceptionMapper();
        var exception = new CustomExceptionWithAllDetailsException("Custom unhandled failure", "ORD-123",
        [
            new KyrolusErrorItem("Balance", "insufficient", "Insufficient balance")
        ]);

        var handled = mapper.TryMap(exception, TestContext, out var mapping);

        handled.ShouldBeTrue();
        mapping.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.InternalError);
        mapping.Error.Errors.ShouldNotBeNull();
        mapping.Error.Errors.Count.ShouldBe(1);
        mapping.Error.Errors[0].Code.ShouldBe("insufficient");
        mapping.Error.Metadata.ShouldNotBeNull();
        mapping.Error.Metadata["orderId"].ShouldBe("ORD-123");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should order mappers and localize output")]
    public void KyrolusExceptionMappingService_Should_Order_Mappers_And_Localize()
    {
        var localizer = new TestErrorLocalizer();
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDefaultExceptionMapper(),
            new KyrolusDomainExceptionMapper(),
            new KyrolusFrameworkExceptionMapper()
        };

        var service = new KyrolusExceptionMappingService(mappers, localizer);
        var domainEx = new KyrolusDomainException(HttpStatusCode.BadRequest, "order_failed", "Order Failed", "Stock issue");

        var result = service.Map(domainEx, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Error.Code.ShouldBe("order_failed");
        result.Error.Title.ShouldBe("Localized: Order Failed");
        result.Error.Detail.ShouldBe("Localized: Stock issue");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should preserve original title and detail when localizer returns null")]
    public void KyrolusExceptionMappingService_Should_Preserve_Original_Title_And_Detail_When_Localizer_Returns_Null()
    {
        var localizer = new TestNullErrorLocalizer();
        var mappers = new IKyrolusExceptionMapper[] { new KyrolusDomainExceptionMapper() };

        var service = new KyrolusExceptionMappingService(mappers, localizer);
        var domainEx = new KyrolusDomainException(HttpStatusCode.BadRequest, "unknown_code", "Original Title", "Original Detail");

        var result = service.Map(domainEx, TestContext);

        result.Error.Title.ShouldBe("Original Title");
        result.Error.Detail.ShouldBe("Original Detail");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should fallback to default internal error when no mappers match")]
    public void KyrolusExceptionMappingService_Should_Fallback_When_No_Mappers_Match()
    {
        var service = new KyrolusExceptionMappingService([]);
        var unhandledEx = new DivideByZeroException("Math error");

        var result = service.Map(unhandledEx, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        result.Error.Code.ShouldBe(KyrolusErrorCodes.InternalError);
        result.Error.Title.ShouldBe("Internal server error");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should exhaust all mappers and fallback when none matches")]
    public void KyrolusExceptionMappingService_Should_Exhaust_All_Mappers_And_Fallback_When_None_Matches()
    {
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDomainExceptionMapper(),
            new KyrolusFrameworkExceptionMapper()
        };

        var service = new KyrolusExceptionMappingService(mappers);
        var unhandledEx = new DivideByZeroException("Unhandled calculation error");

        var result = service.Map(unhandledEx, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        result.Error.Code.ShouldBe(KyrolusErrorCodes.InternalError);
        result.Error.Title.ShouldBe("Internal server error");
        result.Error.TraceId.ShouldBe(TestContext.TraceId);
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should execute custom mapper for custom domain exception")]
    public void KyrolusExceptionMappingService_Should_Execute_Custom_Mapper_For_Custom_Exception()
    {
        var customMapper = new TestOrderLockedMapper();
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDefaultExceptionMapper(),
            new KyrolusFrameworkExceptionMapper(),
            customMapper
        };

        var service = new KyrolusExceptionMappingService(mappers);
        var customException = new TestOrderLockedException("ORD-789", "User-33", "Order is locked by another operator");

        var result = service.Map(customException, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.Error.Code.ShouldBe("order_locked");
        result.Error.Title.ShouldBe("Order Locked");
        result.Error.Metadata.ShouldNotBeNull();
        result.Error.Metadata["orderId"].ShouldBe("ORD-789");
        result.Error.Metadata["lockedBy"].ShouldBe("User-33");
        result.Error.Errors.ShouldNotBeNull();
        result.Error.Errors.Count.ShouldBe(1);
        result.Error.Errors[0].Code.ShouldBe("lock_active");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should map domain exception with errors and metadata")]
    public void KyrolusExceptionMappingService_Should_Map_Domain_Exception_With_Errors_And_Metadata()
    {
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDomainExceptionMapper()
        };

        var service = new KyrolusExceptionMappingService(mappers);
        var domainEx = new KyrolusDomainException(
            statusCode: HttpStatusCode.UnprocessableEntity,
            code: "order_validation_error",
            title: "Order Validation Error",
            detail: "Order details are invalid",
            errors: [new KyrolusErrorItem("Quantity", "invalid_quantity", "Quantity must be > 0")],
            metadata: new Dictionary<string, object?> { ["orderId"] = "ORD-1" });

        var result = service.Map(domainEx, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.Error.Code.ShouldBe("order_validation_error");
        result.Error.Errors.ShouldNotBeNull();
        result.Error.Errors.Count.ShouldBe(1);
        result.Error.Errors[0].Code.ShouldBe("invalid_quantity");
        result.Error.Metadata.ShouldNotBeNull();
        result.Error.Metadata["orderId"].ShouldBe("ORD-1");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should fallback and extract errors from custom exception with errors")]
    public void KyrolusExceptionMappingService_Should_Fallback_And_Extract_Errors_From_Custom_Exception()
    {
        var service = new KyrolusExceptionMappingService([]);
        var customEx = new CustomExceptionWithAllDetailsException("Custom unhandled", "ORD-555",
        [
            new KyrolusErrorItem("Card", "expired", "Card is expired")
        ]);

        var result = service.Map(customEx, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        result.Error.Errors.ShouldNotBeNull();
        result.Error.Errors.Count.ShouldBe(1);
        result.Error.Errors[0].Code.ShouldBe("expired");
        result.Error.Metadata.ShouldNotBeNull();
        result.Error.Metadata["orderId"].ShouldBe("ORD-555");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should fallback and extract errors from KyrolusException")]
    public void KyrolusExceptionMappingService_Should_Fallback_And_Extract_Errors_From_KyrolusException()
    {
        var service = new KyrolusExceptionMappingService([]);
        var kyrolusEx = new KyrolusValidationException(
        [
            new KyrolusErrorItem("Phone", "invalid", "Invalid phone")
        ]);

        var result = service.Map(kyrolusEx, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        result.Error.Errors.ShouldNotBeNull();
        result.Error.Errors.Count.ShouldBe(1);
        result.Error.Errors[0].Code.ShouldBe("invalid");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should unwrap TargetInvocationException and map inner exception")]
    public void KyrolusExceptionMappingService_Should_Unwrap_TargetInvocationException()
    {
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDomainExceptionMapper(),
            new KyrolusFrameworkExceptionMapper()
        };

        var service = new KyrolusExceptionMappingService(mappers);
        var domainEx = new KyrolusDomainException(HttpStatusCode.NotFound, "user_not_found", "User Not Found", "User 42 does not exist");
        var tie = new System.Reflection.TargetInvocationException("Invocation error", domainEx);

        var result = service.Map(tie, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.Error.Code.ShouldBe("user_not_found");
        result.Error.Title.ShouldBe("User Not Found");
    }

    [Fact(DisplayName = "KyrolusExceptionMappingService should unwrap single AggregateException and map inner exception")]
    public void KyrolusExceptionMappingService_Should_Unwrap_Single_AggregateException()
    {
        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDomainExceptionMapper(),
            new KyrolusFrameworkExceptionMapper()
        };

        var service = new KyrolusExceptionMappingService(mappers);
        var argEx = new ArgumentException("Invalid price specified");
        var aggregate = new AggregateException("Task failed", argEx);

        var result = service.Map(aggregate, TestContext);

        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Error.Code.ShouldBe(KyrolusErrorCodes.BadRequest);
    }

    public sealed class TestOrderLockedException(string orderId, string lockedBy, string reason)
        : Exception($"Order {orderId} is locked: {reason}"), IKyrolusExceptionWithMetadata, IKyrolusExceptionWithErrors
    {
        public IReadOnlyDictionary<string, object?> GetMetadata() => new Dictionary<string, object?>
        {
            ["orderId"] = orderId,
            ["lockedBy"] = lockedBy
        };

        public IReadOnlyList<KyrolusErrorItem>? GetErrors() =>
        [
            new KyrolusErrorItem("OrderId", "lock_active", reason)
        ];
    }

    private sealed class TestOrderLockedMapper : IKyrolusExceptionMapper
    {
        public int Order => -200;

        public bool TryMap(Exception exception, KyrolusErrorContext context, out KyrolusExceptionMapping mapping)
        {
            if (exception is not TestOrderLockedException orderLocked)
            {
                mapping = null!;
                return false;
            }

            mapping = KyrolusExceptionMapping.Create(
                code: "order_locked",
                title: "Order Locked",
                statusCode: HttpStatusCode.Conflict,
                detail: orderLocked.Message,
                traceId: context.TraceId,
                errors: orderLocked.GetErrors(),
                metadata: KyrolusMetadataExtractor.Extract(orderLocked));

            return true;
        }
    }

    private sealed class CustomArgumentWithErrorsException(string message, IReadOnlyList<KyrolusErrorItem> errors)
        : ArgumentException(message), IKyrolusExceptionWithErrors
    {
        public IReadOnlyList<KyrolusErrorItem>? GetErrors() => errors;
    }

    public sealed class CustomExceptionWithAllDetailsException(
        string message,
        string orderId,
        IReadOnlyList<KyrolusErrorItem> errors)
        : Exception(message), IKyrolusExceptionWithMetadata, IKyrolusExceptionWithErrors
    {
        public IReadOnlyDictionary<string, object?> GetMetadata() => new Dictionary<string, object?>
        {
            ["orderId"] = orderId
        };

        public IReadOnlyList<KyrolusErrorItem>? GetErrors() => errors;
    }

    private sealed class TestErrorLocalizer : IKyrolusErrorLocalizer
    {
        public string? Localize(string code, string? defaultMessage, CultureInfo? culture)
            => $"Localized: {defaultMessage}";
    }

    private sealed class TestNullErrorLocalizer : IKyrolusErrorLocalizer
    {
        public string? Localize(string code, string? defaultMessage, CultureInfo? culture) => null;
    }
}
