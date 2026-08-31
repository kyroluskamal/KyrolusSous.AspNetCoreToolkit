using System.Diagnostics;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests;

public class KyrolusExceptionTranslatorTests
{
    private static KyrolusExceptionTranslator CreateTranslator(
        Action<KyrolusExceptionHandlingOptions>? configureOptions = null,
        IHostEnvironment? hostEnvironment = null,
        IKyrolusLocalizer? errorLocalizer = null)
    {
        var options = new KyrolusExceptionHandlingOptions();
        configureOptions?.Invoke(options);
        var optionsWrapper = Options.Create(options);

        var mappers = new IKyrolusExceptionMapper[]
        {
            new KyrolusDomainExceptionMapper(),
            new KyrolusFrameworkExceptionMapper(),
            new KyrolusDefaultExceptionMapper()
        };
        var mappingService = new KyrolusExceptionMappingService(mappers);
        var sanitizer = new KyrolusDefaultErrorMetadataSanitizer(optionsWrapper);
        var environment = hostEnvironment ?? new TestHostEnvironment("Development");

        return new KyrolusExceptionTranslator(mappingService, sanitizer, environment, optionsWrapper, errorLocalizer);
    }

    [Fact(DisplayName = "TranslateToMapping with custom context and explicit includeDetails should enrich mapping")]
    public void TranslateToMapping_With_CustomContext_And_ExplicitDetails()
    {
        var translator = CreateTranslator();
        var context = new KyrolusErrorContext(
            TraceId: "trace-abc-123",
            CorrelationId: "corr-1",
            UserId: "user-1",
            TenantId: "tenant-1",
            Path: "/api/orders",
            Method: "POST",
            Culture: null);

        var ex = new InvalidOperationException("Operation failed");

        var mapping = translator.TranslateToMapping(ex, context, includeDetails: true);

        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        mapping.Error.Code.ShouldBe(KyrolusErrorCodes.InternalError);
        mapping.Error.TraceId.ShouldBe("trace-abc-123");
        mapping.Error.Metadata.ShouldNotBeNull();
        mapping.Error.Metadata.ShouldContainKey("exceptionType");
        mapping.Error.Metadata["exceptionType"]!.ToString().ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact(DisplayName = "TranslateToMapping with null context should create default context and resolve TraceId from Activity")]
    public void TranslateToMapping_With_NullContext_Should_Use_DefaultContext_With_Activity()
    {
        var translator = CreateTranslator();

        using var activity = new Activity("TestActivity").Start();
        var ex = new ArgumentException("Invalid param", "age");

        var mapping = translator.TranslateToMapping(ex, context: null);

        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        mapping.Error.TraceId.ShouldBe(activity.Id);
    }

    [Fact(DisplayName = "TranslateToMapping with IncludeTraceId disabled should not populate TraceId")]
    public void TranslateToMapping_With_IncludeTraceId_Disabled_Should_Have_Null_TraceId()
    {
        var translator = CreateTranslator(opts => opts.IncludeTraceId = false);

        using var activity = new Activity("TestActivity").Start();
        var ex = new ArgumentException("Invalid param", "age");

        var mapping = translator.TranslateToMapping(ex, context: null);

        mapping.ShouldNotBeNull();
        mapping.Error.TraceId.ShouldBeNull();
    }

    [Fact(DisplayName = "TranslateToMapping should sanitize sensitive keys from exception metadata")]
    public void TranslateToMapping_Should_Sanitize_Sensitive_Metadata()
    {
        var translator = CreateTranslator();
        var ex = new KyrolusBadRequestException("Invalid credentials")
            .WithMetadata("password", "SuperSecret123")
            .WithMetadata("accountNumber", "ACC-999");

        var mapping = translator.TranslateToMapping(ex, includeDetails: false);

        mapping.ShouldNotBeNull();
        mapping.Error.Metadata.ShouldNotBeNull();
        mapping.Error.Metadata.ShouldNotContainKey("password");
        mapping.Error.Metadata.ShouldContainKey("accountNumber");
        mapping.Error.Metadata["accountNumber"]!.ToString().ShouldBe("ACC-999");
    }

    [Fact(DisplayName = "Translate should return complete KyrolusErrorResult DTO")]
    public void Translate_Should_Return_KyrolusErrorResult()
    {
        var translator = CreateTranslator();
        var context = new KyrolusErrorContext(
            TraceId: "trace-xyz",
            CorrelationId: "corr-2",
            UserId: "user-2",
            TenantId: "tenant-2",
            Path: "/api/items",
            Method: "GET",
            Culture: null);

        var ex = new KyrolusNotFoundException("Item", "ITEM-404");

        var result = translator.Translate(ex, context, includeDetails: false);

        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.IsTransient.ShouldBeFalse();
        result.ExceptionType.ShouldBe(typeof(KyrolusNotFoundException).FullName);
        result.Error.Code.ShouldBe(KyrolusErrorCodes.NotFound);
        result.Error.Title.ShouldBe("Item not found");
        result.Error.TraceId.ShouldBe("trace-xyz");
    }

    [Fact(DisplayName = "Translate with null context should resolve default context and project into KyrolusErrorResult")]
    public void Translate_With_NullContext_Should_Use_DefaultContext()
    {
        var translator = CreateTranslator();
        var ex = new InvalidOperationException("General system failure");

        var result = translator.Translate(ex);

        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        result.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
        result.Error.Code.ShouldBe(KyrolusErrorCodes.InternalError);
    }

    [Fact(DisplayName = "TranslateToMapping should localize title and detail using injected IKyrolusLocalizer")]
    public void TranslateToMapping_Should_Localize_Title_And_Detail()
    {
        var localizer = new TestCustomLocalizer();
        var translator = CreateTranslator(errorLocalizer: localizer);
        var context = new KyrolusErrorContext(
            TraceId: "trace-xyz",
            CorrelationId: null,
            UserId: null,
            TenantId: null,
            Path: null,
            Method: null,
            Culture: CultureInfo.GetCultureInfo("ar-EG"));

        var domainEx = new KyrolusDomainException(HttpStatusCode.BadRequest, "order_failed", "Order Failed", "Stock issue");

        var mapping = translator.TranslateToMapping(domainEx, context);

        mapping.ShouldNotBeNull();
        mapping.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        mapping.Error.Code.ShouldBe("order_failed");
        mapping.Error.Title.ShouldBe("Localized: order_failed");
        mapping.Error.Detail.ShouldBe("Localized: order_failed.detail");
    }

    private sealed class TestCustomLocalizer : IKyrolusLocalizer
    {
        public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
            => new($"Localized: {key}", ResourceNotFound: false);

        public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
            => GetString(key, culture);

        public string Format(string template, object? arguments) => template;
    }
}
