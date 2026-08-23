using System.Globalization;
using Microsoft.Extensions.FileProviders;

namespace KyrolusSous.ExceptionHandling.Runtime.UnitTests.Helpers;

public class KyrolusExceptionEnrichmentHelperTests
{
    private const string TraceId = "hkjh423jkh4jk23h4jk23";
    private const string CorrelationId = "CoreId";
    private const string UserId = "123";
    private const string TenantId = "456";
    private const string Path = "/api/products/12";

    private static readonly KyrolusExceptionMapping ExceptionMapping = KyrolusExceptionMapping.Create(
        KyrolusErrorCodes.InternalError,
        "Internal Server Error",
        HttpStatusCode.BadRequest,
        "This is test",
        TraceId,
        metadata: new Dictionary<string, object?>
        {
            { "name", "kyrolus" }
        }
    );

    [Fact(DisplayName = "ShouldIncludeDetails returns true when IncludeExceptionDetailsInResponse is true")]
    public void ShouldIncludeDetails_WhenIncludeExceptionDetailsInResponseIsTrue_ShouldReturnTrue()
    {
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeExceptionDetailsInResponse = true
        };

        var result = KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, new TestHostEnvironment("Production"));

        result.ShouldBeTrue();
    }

    [Fact(DisplayName = "ShouldIncludeDetails returns true in Development when IncludeExceptionDetailsInDevelopment is true")]
    public void ShouldIncludeDetails_WhenInDevelopmentAndIncludeInDevIsTrue_ShouldReturnTrue()
    {
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeExceptionDetailsInResponse = false,
            IncludeExceptionDetailsInDevelopment = true
        };

        var result = KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, new TestHostEnvironment("Development"));

        result.ShouldBeTrue();
    }

    [Fact(DisplayName = "ShouldIncludeDetails returns false in Production when IncludeExceptionDetailsInResponse is false")]
    public void ShouldIncludeDetails_WhenInProductionAndIncludeInResponseIsFalse_ShouldReturnFalse()
    {
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeExceptionDetailsInResponse = false,
            IncludeExceptionDetailsInDevelopment = true
        };

        var result = KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, new TestHostEnvironment("Production"));

        result.ShouldBeFalse();
    }

    [Fact(DisplayName = "ShouldIncludeDetails returns false in Development when IncludeExceptionDetailsInDevelopment is false")]
    public void ShouldIncludeDetails_WhenInDevelopmentAndIncludeInDevIsFalse_ShouldReturnFalse()
    {
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeExceptionDetailsInResponse = false,
            IncludeExceptionDetailsInDevelopment = false
        };

        var result = KyrolusExceptionEnrichmentHelper.ShouldIncludeDetails(options, new TestHostEnvironment("Development"));

        result.ShouldBeFalse();
    }

    [Fact(DisplayName = "ApplyExceptionDetails adds correlationId, userId, and tenantId when IncludeContextMetadata is true")]
    public void ApplyExceptionDetails_WhenIncludeContextMetadataIsTrue_ShouldAddContextMetadata()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = true
        };

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            ExceptionMapping, ex, errorContext, options, new TestMetadataSanitizer(), includeDetails: false);

        mapped.Error.Metadata.ShouldNotBeNull();
        mapped.Error.Metadata["correlationId"].ShouldBe(CorrelationId);
        mapped.Error.Metadata["userId"].ShouldBe(UserId);
        mapped.Error.Metadata["tenantId"].ShouldBe(TenantId);
        mapped.Error.Metadata["name"].ShouldBe("kyrolus");
        mapped.Error.Metadata.ShouldNotContainKey("exceptionType");
        mapped.Error.Metadata.ShouldNotContainKey("stackTrace");
        mapped.Error.Metadata.ShouldNotContainKey("innerException");
    }

    [Fact(DisplayName = "ApplyExceptionDetails does not add context metadata when values are whitespace")]
    public void ApplyExceptionDetails_WhenContextMetadataValuesAreWhitespace_ShouldNotAddThem()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(" ", " ", " ", " ", " ", "POST", null);
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = true
        };

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            ExceptionMapping, ex, errorContext, options, new TestMetadataSanitizer(), includeDetails: false);

        mapped.Error.Metadata.ShouldNotBeNull();
        mapped.Error.Metadata["name"].ShouldBe("kyrolus");
        mapped.Error.Metadata.ShouldNotContainKey("correlationId");
        mapped.Error.Metadata.ShouldNotContainKey("userId");
        mapped.Error.Metadata.ShouldNotContainKey("tenantId");
    }

    [Fact(DisplayName = "ApplyExceptionDetails does not add context metadata when values are null")]
    public void ApplyExceptionDetails_WhenContextMetadataValuesAreNull_ShouldNotAddThem()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(null, null, null, null, null, "POST", null);
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = true
        };

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            ExceptionMapping, ex, errorContext, options, new TestMetadataSanitizer(), includeDetails: false);

        mapped.Error.Metadata.ShouldNotBeNull();
        mapped.Error.Metadata["name"].ShouldBe("kyrolus");
        mapped.Error.Metadata.ShouldNotContainKey("correlationId");
        mapped.Error.Metadata.ShouldNotContainKey("userId");
        mapped.Error.Metadata.ShouldNotContainKey("tenantId");
    }

    [Fact(DisplayName = "ApplyExceptionDetails adds exceptionType and stackTrace when includeDetails is true")]
    public void ApplyExceptionDetails_WhenIncludeDetailsIsTrue_ShouldAddExceptionDetails()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            ExceptionMapping, ex, errorContext, options, new TestMetadataSanitizer(), includeDetails: true);

        mapped.Error.Metadata.ShouldNotBeNull();
        mapped.Error.Metadata["exceptionType"].ShouldBe(ex.GetType().FullName);
        mapped.Error.Metadata["stackTrace"].ShouldBe(ex.StackTrace);
        mapped.Error.Metadata["name"].ShouldBe("kyrolus");
        mapped.Error.Metadata.ShouldNotContainKey("correlationId");
        mapped.Error.Metadata.ShouldNotContainKey("userId");
        mapped.Error.Metadata.ShouldNotContainKey("tenantId");
        mapped.Error.Metadata.ShouldNotContainKey("innerException");
    }

    [Fact(DisplayName = "ApplyExceptionDetails adds innerException message when exception has innerException and includeDetails is true")]
    public void ApplyExceptionDetails_WhenExceptionHasInnerException_ShouldAddInnerExceptionMessage()
    {
        var inner = new Exception("InnerExceptionMessage");
        var ex = new Exception("OuterException", inner);
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            ExceptionMapping, ex, errorContext, options, new TestMetadataSanitizer(), includeDetails: true);

        mapped.Error.Metadata.ShouldNotBeNull();
        mapped.Error.Metadata["innerException"].ShouldBe("InnerExceptionMessage");
    }

    [Fact(DisplayName = "ApplyExceptionDetails does not add innerException when innerException is null and includeDetails is true")]
    public void ApplyExceptionDetails_WhenExceptionHasNoInnerException_ShouldNotAddInnerException()
    {
        var ex = new Exception("OuterExceptionWithoutInner");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            ExceptionMapping, ex, errorContext, options, new TestMetadataSanitizer(), includeDetails: true);

        mapped.Error.Metadata.ShouldNotBeNull();
        mapped.Error.Metadata.ShouldNotContainKey("innerException");
    }

    [Fact(DisplayName = "ApplyExceptionDetails returns original mapping instance without allocations when metadata is empty")]
    public void ApplyExceptionDetails_WhenMetadataCountIsZero_ShouldReturnOriginalMappingInstance()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var mappingWithoutMetadata = ExceptionMapping with
        {
            Error = ExceptionMapping.Error with { Metadata = null }
        };

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            mappingWithoutMetadata, ex, errorContext, options, new TestMetadataSanitizer(), includeDetails: false);

        mapped.Error.Metadata.ShouldBeNull();
        mapped.ShouldBeSameAs(mappingWithoutMetadata);
    }

    [Fact(DisplayName = "ApplyExceptionDetails applies metadata sanitizer to filter sensitive keys")]
    public void ApplyExceptionDetails_ShouldSanitizeMetadataUsingProvidedSanitizer()
    {
        var mappingWithSecrets = KyrolusExceptionMapping.Create(
            KyrolusErrorCodes.InternalError, "Error", HttpStatusCode.InternalServerError,
            "Test", TraceId, metadata: new Dictionary<string, object?>
            {
                { "password", "SuperSecret123" },
                { "safeKey", "safeValue" }
            });

        var ex = new Exception("Error");
        var errorContext = new KyrolusErrorContext(TraceId, null, null, null, Path, "POST", null);
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var realSanitizer = new KyrolusDefaultErrorMetadataSanitizer(Options.Create(options));

        var mapped = KyrolusExceptionEnrichmentHelper.ApplyExceptionDetails(
            mappingWithSecrets, ex, errorContext, options, realSanitizer, includeDetails: false);

        mapped.Error.Metadata.ShouldNotBeNull();
        mapped.Error.Metadata.ShouldNotContainKey("password");
        mapped.Error.Metadata["safeKey"].ShouldBe("safeValue");
    }

    private sealed class TestHostEnvironment(string environmentName = "Development") : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class TestMetadataSanitizer : IKyrolusErrorMetadataSanitizer
    {
        public IReadOnlyDictionary<string, object?> Sanitize(
            IReadOnlyDictionary<string, object?> metadata,
            KyrolusErrorContext context)
            => metadata;
    }
}
