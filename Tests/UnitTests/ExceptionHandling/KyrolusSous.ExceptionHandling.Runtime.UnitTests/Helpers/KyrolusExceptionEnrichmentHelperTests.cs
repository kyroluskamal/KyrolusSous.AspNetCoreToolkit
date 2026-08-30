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

    [Fact(DisplayName = "EnrichMetadata adds correlationId, userId, and tenantId when IncludeContextMetadata is true")]
    public void EnrichMetadata_WhenIncludeContextMetadataIsTrue_ShouldAddContextMetadata()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = true
        };

        var metadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(
            new Dictionary<string, object?> { ["name"] = "kyrolus" }, ex, errorContext, options, includeDetails: false);

        metadata.ShouldNotBeNull();
        metadata["correlationId"].ShouldBe(CorrelationId);
        metadata["userId"].ShouldBe(UserId);
        metadata["tenantId"].ShouldBe(TenantId);
        metadata["name"].ShouldBe("kyrolus");
        metadata.ShouldNotContainKey("exceptionType");
        metadata.ShouldNotContainKey("stackTrace");
        metadata.ShouldNotContainKey("innerException");
    }

    [Fact(DisplayName = "EnrichMetadata does not add context metadata when values are whitespace")]
    public void EnrichMetadata_WhenContextMetadataValuesAreWhitespace_ShouldNotAddThem()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(" ", " ", " ", " ", " ", "POST", null);
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = true
        };

        var metadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(
            new Dictionary<string, object?> { ["name"] = "kyrolus" }, ex, errorContext, options, includeDetails: false);

        metadata.ShouldNotBeNull();
        metadata["name"].ShouldBe("kyrolus");
        metadata.ShouldNotContainKey("correlationId");
        metadata.ShouldNotContainKey("userId");
        metadata.ShouldNotContainKey("tenantId");
    }

    [Fact(DisplayName = "EnrichMetadata does not add context metadata when values are null")]
    public void EnrichMetadata_WhenContextMetadataValuesAreNull_ShouldNotAddThem()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(null, null, null, null, null, "POST", null);
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = true
        };

        var metadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(
            new Dictionary<string, object?> { ["name"] = "kyrolus" }, ex, errorContext, options, includeDetails: false);

        metadata.ShouldNotBeNull();
        metadata["name"].ShouldBe("kyrolus");
        metadata.ShouldNotContainKey("correlationId");
        metadata.ShouldNotContainKey("userId");
        metadata.ShouldNotContainKey("tenantId");
    }

    [Fact(DisplayName = "EnrichMetadata adds exceptionType and stackTrace when includeDetails is true")]
    public void EnrichMetadata_WhenIncludeDetailsIsTrue_ShouldAddExceptionDetails()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var metadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(
            new Dictionary<string, object?> { ["name"] = "kyrolus" }, ex, errorContext, options, includeDetails: true);

        metadata.ShouldNotBeNull();
        metadata["exceptionType"].ShouldBe(ex.GetType().FullName);
        metadata["stackTrace"].ShouldBe(ex.StackTrace);
        metadata["name"].ShouldBe("kyrolus");
        metadata.ShouldNotContainKey("correlationId");
        metadata.ShouldNotContainKey("userId");
        metadata.ShouldNotContainKey("tenantId");
        metadata.ShouldNotContainKey("innerException");
    }

    [Fact(DisplayName = "EnrichMetadata adds innerException message when exception has innerException and includeDetails is true")]
    public void EnrichMetadata_WhenExceptionHasInnerException_ShouldAddInnerExceptionMessage()
    {
        var inner = new Exception("InnerExceptionMessage");
        var ex = new Exception("OuterException", inner);
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var metadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(
            null, ex, errorContext, options, includeDetails: true);

        metadata.ShouldNotBeNull();
        metadata["innerException"].ShouldBe("InnerExceptionMessage");
    }

    [Fact(DisplayName = "EnrichMetadata does not add innerException when innerException is null and includeDetails is true")]
    public void EnrichMetadata_WhenExceptionHasNoInnerException_ShouldNotAddInnerException()
    {
        var ex = new Exception("OuterExceptionWithoutInner");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var metadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(
            null, ex, errorContext, options, includeDetails: true);

        metadata.ShouldNotBeNull();
        metadata.ShouldNotContainKey("innerException");
    }

    [Fact(DisplayName = "EnrichMetadata returns empty metadata when base is null and no enrichment options enabled")]
    public void EnrichMetadata_WhenBaseIsNullAndNoOptions_ShouldReturnEmptyDictionary()
    {
        var ex = new Exception("This is test Exception");
        var errorContext = new KyrolusErrorContext(TraceId, CorrelationId, UserId, TenantId, Path, "POST", new CultureInfo("en-US"));
        var options = new KyrolusExceptionHandlingOptions
        {
            IncludeContextMetadata = false
        };

        var metadata = KyrolusExceptionEnrichmentHelper.EnrichMetadata(
            null, ex, errorContext, options, includeDetails: false);

        metadata.ShouldNotBeNull();
        metadata.Count.ShouldBe(0);
    }
}
