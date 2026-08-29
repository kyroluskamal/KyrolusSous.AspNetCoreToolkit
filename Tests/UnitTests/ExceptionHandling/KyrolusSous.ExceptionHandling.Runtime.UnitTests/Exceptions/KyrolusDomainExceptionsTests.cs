namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Exceptions;

public class KyrolusDomainExceptionsTests
{
    private static readonly KyrolusErrorContext TestErrorContext = new(
        TraceId: "trace-domain-123",
        CorrelationId: "corr-456",
        UserId: "user-789",
        TenantId: "tenant-101",
        Path: "/api/domain",
        Method: "GET",
        Culture: null);

    public static TheoryData<KyrolusException, HttpStatusCode, string, string, bool, bool> DomainExceptionTestData =>
        new()
        {
            {
                new KyrolusNotFoundException("Product", "PROD-100"),
                HttpStatusCode.NotFound,
                KyrolusErrorCodes.NotFound,
                "Product not found",
                false,
                false
            },
            {
                new KyrolusNotFoundException("Order", 42),
                HttpStatusCode.NotFound,
                KyrolusErrorCodes.NotFound,
                "Order not found",
                false,
                false
            },
            {
                new KyrolusConflictException("Email already in use", "The email is registered"),
                HttpStatusCode.Conflict,
                KyrolusErrorCodes.Conflict,
                "Email already in use",
                false,
                false
            },
            {
                new KyrolusForbiddenException("Admin access required"),
                HttpStatusCode.Forbidden,
                KyrolusErrorCodes.Forbidden,
                "Forbidden",
                false,
                false
            },
            {
                new KyrolusUnauthorizedException("Token expired"),
                HttpStatusCode.Unauthorized,
                KyrolusErrorCodes.Unauthorized,
                "Unauthorized",
                false,
                false
            },
            {
                new KyrolusBadRequestException("Invalid request format", "Payload was invalid"),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.BadRequest,
                "Invalid request format",
                false,
                false
            },
            {
                new KyrolusRateLimitException("Too many requests per minute"),
                (HttpStatusCode)429,
                KyrolusErrorCodes.RateLimit,
                "Rate limit exceeded",
                true,
                false
            },
            {
                new KyrolusTimeoutException("Database connection timeout"),
                HttpStatusCode.GatewayTimeout,
                KyrolusErrorCodes.Timeout,
                "Timeout",
                true,
                true
            },
            {
                new KyrolusExternalServiceException("PaymentGateway", "Gateway returned 500"),
                HttpStatusCode.BadGateway,
                KyrolusErrorCodes.ExternalService,
                "PaymentGateway failure",
                true,
                true
            },
            {
                new KyrolusDomainException("order_rejected", "Order was rejected"),
                HttpStatusCode.BadRequest,
                "order_rejected",
                "order_rejected",
                false,
                true
            },
            {
                new KyrolusDomainException("payment_failed", "Payment gateway timed out", null, isTransient: true),
                HttpStatusCode.BadRequest,
                "payment_failed",
                "payment_failed",
                true,
                true
            },
            {
                new KyrolusDomainException(
                    HttpStatusCode.Conflict,
                    "item_locked",
                    "Item Locked",
                    "Item is locked",
                    [new KyrolusErrorItem("ItemId", "locked", "Locked by user")],
                    new Dictionary<string, object?> { ["itemId"] = "ITM-1" },
                    isTransient: false,
                    shouldLog: false),
                HttpStatusCode.Conflict,
                "item_locked",
                "Item Locked",
                false,
                false
            },
            {
                new KyrolusValidationException([new KyrolusErrorItem("Email", "invalid", "Invalid email")]),
                HttpStatusCode.BadRequest,
                KyrolusErrorCodes.Validation,
                "Validation failed",
                false,
                false
            }
        };

    [Theory(DisplayName = "KyrolusDomainExceptionMapper should correctly map all built-in domain exception types")]
    [MemberData(nameof(DomainExceptionTestData))]
    public void DomainExceptionMapper_Should_Map_All_BuiltIn_Exceptions(
        KyrolusException exception,
        HttpStatusCode expectedStatusCode,
        string expectedCode,
        string expectedTitle,
        bool expectedIsTransient,
        bool expectedShouldLog)
    {
        var mapper = new KyrolusDomainExceptionMapper();

        var handled = mapper.TryMap(exception, TestErrorContext, out var mapping);

        handled.ShouldBeTrue();
        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(expectedStatusCode);
        mapping.Error.Code.ShouldBe(expectedCode);
        mapping.Error.Title.ShouldBe(expectedTitle);
        mapping.IsTransient.ShouldBe(expectedIsTransient);
        mapping.ShouldLog.ShouldBe(expectedShouldLog);
    }

    [Fact(DisplayName = "KyrolusNotFoundException should expose EntityName and Key properties and set metadata")]
    public void NotFoundException_Should_Expose_Properties_And_Metadata()
    {
        var inner = new InvalidOperationException("Inner DB issue");
        var ex = new KyrolusNotFoundException("Customer", "CUST-999", inner);

        ex.EntityName.ShouldBe("Customer");
        ex.Key.ShouldBe("CUST-999");
        ex.InnerException.ShouldBe(inner);
        ex.Metadata.ShouldNotBeNull();
        ex.Metadata["entityName"].ShouldBe("Customer");
        ex.Metadata["key"].ShouldBe("CUST-999");
        ex.Detail.ShouldBe("Customer with key 'CUST-999' was not found.");
    }

    [Fact(DisplayName = "KyrolusExternalServiceException should expose ServiceName property and set metadata")]
    public void ExternalServiceException_Should_Expose_Properties_And_Metadata()
    {
        var inner = new HttpRequestException("Socket closed");
        var ex = new KyrolusExternalServiceException("StripeAPI", "Card charge timeout", inner);

        ex.ServiceName.ShouldBe("StripeAPI");
        ex.InnerException.ShouldBe(inner);
        ex.Metadata.ShouldNotBeNull();
        ex.Metadata["serviceName"].ShouldBe("StripeAPI");
        ex.Detail.ShouldBe("Card charge timeout");
        ex.IsTransient.ShouldBeTrue();
        ex.ShouldLog.ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusException fluent builder methods should correctly modify exception state")]
    public void KyrolusException_Fluent_Builder_Methods_Should_Work()
    {
        var ex = new KyrolusBadRequestException("Initial Title", "Initial Detail")
            .WithMetadata("orderId", "ORD-123")
            .WithMetadata(new Dictionary<string, object?> { ["source"] = "checkout" })
            .WithError("CouponCode", "Coupon expired", "expired_coupon")
            .WithDetail("Updated Detail")
            .AsTransient(true)
            .WithoutLogging();

        ex.Detail.ShouldBe("Updated Detail");
        ex.IsTransient.ShouldBeTrue();
        ex.ShouldLog.ShouldBeFalse();
        ex.Metadata.ShouldNotBeNull();
        ex.Metadata["orderId"].ShouldBe("ORD-123");
        ex.Metadata["source"].ShouldBe("checkout");
        ex.Errors.ShouldNotBeNull();
        ex.Errors.Count.ShouldBe(1);
        ex.Errors[0].Field.ShouldBe("CouponCode");
        ex.Errors[0].Message.ShouldBe("Coupon expired");
        ex.Errors[0].Code.ShouldBe("expired_coupon");

        ex.WithLogging(true);
        ex.ShouldLog.ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusException WithMetadata with dictionary should initialize metadata when initially null")]
    public void KyrolusException_WithMetadata_Dictionary_Should_Initialize_When_Null()
    {
        var ex = new KyrolusBadRequestException("Title", "Detail");
        var metadata = new Dictionary<string, object?> { ["tenantId"] = "TEN-100" };

        ex.WithMetadata(metadata);

        ex.Metadata.ShouldNotBeNull();
        ex.Metadata["tenantId"].ShouldBe("TEN-100");
    }

    [Fact(DisplayName = "KyrolusException WithMetadata should throw on null or invalid inputs")]
    public void KyrolusException_WithMetadata_Should_Throw_On_Invalid_Inputs()
    {
        var ex = new KyrolusBadRequestException("Title", "Detail");

        Should.Throw<ArgumentNullException>(() => ex.WithMetadata((IReadOnlyDictionary<string, object?>)null!));
        Should.Throw<ArgumentException>(() => ex.WithMetadata(null!, "val"));
        Should.Throw<ArgumentException>(() => ex.WithMetadata("", "val"));
        Should.Throw<ArgumentException>(() => ex.WithMetadata("   ", "val"));
    }

    [Fact(DisplayName = "KyrolusErrorCodeRegistryException should preserve error message")]
    public void ErrorCodeRegistryException_Should_Preserve_Message()
    {
        var ex = new KyrolusErrorCodeRegistryException("Duplicate code registered");

        ex.Message.ShouldBe("Duplicate code registered");
    }

    [Fact(DisplayName = "KyrolusDomainException should throw KyrolusErrorCodeRegistryException when StrictMode is enabled and code is unregistered")]
    public void DomainException_Should_Throw_When_StrictMode_Enabled_And_Code_Unregistered()
    {
        try
        {
            KyrolusErrorCodeRegistry.EnableStrictMode();

            var ex = Should.Throw<KyrolusErrorCodeRegistryException>(() =>
                _ = new KyrolusDomainException("unregistered_domain_code", "Detail message"));

            ex.Message.ShouldContain("Strict Mode Violation");
            ex.Message.ShouldContain("unregistered_domain_code");
        }
        finally
        {
            KyrolusErrorCodeRegistry.ResetToDefault();
        }
    }
}
